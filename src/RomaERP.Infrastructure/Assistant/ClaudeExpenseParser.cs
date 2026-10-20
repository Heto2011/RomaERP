using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Assistant;

public class ClaudeExpenseParser : IClaudeExpenseParser
{
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly ClaudeSettings _settings;
    private readonly IAiUsageLimiter _usageLimiter;
    private readonly IUserLanguage _language;

    public ClaudeExpenseParser(HttpClient httpClient, IOptions<ClaudeSettings> settings, IAiUsageLimiter usageLimiter, IUserLanguage language)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _usageLimiter = usageLimiter;
        _language = language;
    }

    public async Task<ExpenseExtractionResult> ExtractAsync(
        string userMessage,
        string? priorContext,
        IReadOnlyList<ExpenseAccountCandidate> expenseAccounts,
        CancellationToken ct = default)
    {
        var accountsList = string.Join("\n", expenseAccounts.Select(a => $"- {a.Code}: {a.NameAr}"));
        var systemPrompt = $"""
            أنت مساعد محاسبي بيساعد موظف يسجّل مصروفات الشركة عن طريق الكلام العادي (مصري أو عربي أو إنجليزي).
            مهمتك تستخرج من رسالة المستخدم: المبلغ، وصف قصير للمصروف، وأنسب حساب مصروفات من القائمة دي:
            {accountsList}

            لو المبلغ مش واضح أو مذكورش، اطلب توضيح بسؤال عربي قصير ومباشر.
            استخدم أداة extract_expense دايمًا للرد.
            """;

        var messageContent = priorContext is null ? userMessage : $"(الرسالة السابقة: {priorContext})\n{userMessage}";
        var userContent = JsonValue.Create(messageContent);

        return await SendExtractionRequestAsync(systemPrompt, userContent!, ct);
    }

    public async Task<ExpenseExtractionResult> ExtractFromReceiptImageAsync(
        byte[] imageBytes,
        string mediaType,
        IReadOnlyList<ExpenseAccountCandidate> expenseAccounts,
        CancellationToken ct = default)
    {
        var accountsList = string.Join("\n", expenseAccounts.Select(a => $"- {a.Code}: {a.NameAr}"));
        var systemPrompt = $"""
            أنت مساعد محاسبي بيساعد موظف يسجّل مصروف الشركة عن طريق صورة إيصال أو فاتورة.
            اقرا الصورة المرفقة، واستخرج منها: المبلغ الإجمالي، اسم المحل/المورد كوصف قصير للمصروف،
            تاريخ الإيصال لو ظاهر وواضح (بصيغة yyyy-MM-dd)، وأنسب حساب مصروفات من القائمة دي:
            {accountsList}

            لو الصورة مش واضحة أو المبلغ مش مقروء، اطلب توضيح بسؤال عربي قصير ومباشر بدل ما تخمّن رقم.
            استخدم أداة extract_expense دايمًا للرد.
            """;

        var userContent = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = Convert.ToBase64String(imageBytes)
                }
            },
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = "استخرج بيانات المصروف من صورة الإيصال دي."
            }
        };

        return await SendExtractionRequestAsync(systemPrompt, userContent, ct, includeEntryDate: true);
    }

    public async Task<InvoiceScanExtraction> ExtractInvoiceFromImageAsync(
        byte[] imageBytes,
        string mediaType,
        IReadOnlyList<ExpenseAccountCandidate> expenseAccounts,
        CancellationToken ct = default)
    {
        var accountsList = string.Join("\n", expenseAccounts.Select(a => $"- {a.Code}: {a.NameAr}"));
        var systemPrompt = $"""
            أنت مساعد محاسبي بيقرا صورة فاتورة مورد (مشتريات) ويستخرج بياناتها بدقة عشان تتعبّى في نموذج فاتورة المشتريات تلقائيًا.
            استخرج: اسم المورد زي ما هو مكتوب، الرقم الضريبي للمورد لو ظاهر، رقم الفاتورة، تاريخ الفاتورة (yyyy-MM-dd)،
            العملة، الإجمالي قبل الضريبة (subtotal)، قيمة الضريبة (vat_amount)، نسبة الضريبة، الإجمالي النهائي (total)،
            وبنود الفاتورة (الوصف والكمية وسعر الوحدة قبل الضريبة) لو واضحة.
            وكمان رشّح أنسب حساب (تصنيف محاسبي) للفاتورة من القائمة دي:
            {accountsList}

            لا تخمّن أي رقم مش مقروء — سيبه فاضي. لو الصورة مش فاتورة أو مش مقروءة، خلّي needs_clarification = true
            واكتب سؤال/ملاحظة عربي قصيرة. استخدم أداة extract_purchase_invoice دايمًا للرد.
            """;

        var userContent = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = Convert.ToBase64String(imageBytes)
                }
            },
            new JsonObject { ["type"] = "text", ["text"] = "استخرج بيانات فاتورة المورد دي." }
        };

        var nullableNumber = () => new JsonObject { ["type"] = new JsonArray { "number", "null" } };
        var nullableString = () => new JsonObject { ["type"] = new JsonArray { "string", "null" } };
        var properties = new JsonObject
        {
            ["vendor_name"] = nullableString(),
            ["vendor_tax_number"] = nullableString(),
            ["invoice_number"] = nullableString(),
            ["invoice_date"] = new JsonObject { ["type"] = new JsonArray { "string", "null" }, ["description"] = "yyyy-MM-dd" },
            ["currency"] = nullableString(),
            ["subtotal"] = nullableNumber(),
            ["vat_amount"] = nullableNumber(),
            ["vat_rate_percent"] = nullableNumber(),
            ["total"] = nullableNumber(),
            ["suggested_account_code"] = nullableString(),
            ["lines"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["description"] = new JsonObject { ["type"] = "string" },
                        ["quantity"] = nullableNumber(),
                        ["unit_price"] = nullableNumber()
                    }
                }
            },
            ["needs_clarification"] = new JsonObject { ["type"] = "boolean" },
            ["clarifying_question"] = nullableString()
        };

        var input = await CallToolAsync(
            systemPrompt, userContent, "extract_purchase_invoice",
            "Extract structured supplier-invoice fields from a photo of a purchase invoice.",
            properties, new JsonArray { "needs_clarification" }, ct);

        DateTime? invoiceDate = ReadString(input, "invoice_date") is { } dateText && DateTime.TryParse(dateText, out var parsed) ? parsed.Date : null;

        var lines = new List<InvoiceScanLine>();
        if (input["lines"] is JsonArray lineArray)
        {
            foreach (var node in lineArray.OfType<JsonObject>())
            {
                var description = ReadString(node, "description");
                if (description is null) continue;
                lines.Add(new InvoiceScanLine(description, ReadNumber(node, "quantity"), ReadNumber(node, "unit_price")));
            }
        }

        var subtotal = ReadNumber(input, "subtotal");
        var total = ReadNumber(input, "total");
        return new InvoiceScanExtraction(
            VendorName: ReadString(input, "vendor_name"),
            VendorTaxNumber: ReadString(input, "vendor_tax_number"),
            InvoiceNumber: ReadString(input, "invoice_number"),
            InvoiceDate: invoiceDate,
            Currency: ReadString(input, "currency"),
            SubTotal: subtotal,
            VatAmount: ReadNumber(input, "vat_amount"),
            VatRatePercent: ReadNumber(input, "vat_rate_percent"),
            Total: total,
            SuggestedAccountCode: ReadString(input, "suggested_account_code"),
            Lines: lines,
            NeedsClarification: input["needs_clarification"]?.GetValue<bool>() ?? (subtotal is null && total is null),
            ClarifyingQuestion: ReadString(input, "clarifying_question"));
    }

    public async Task<MenuScanExtraction> ExtractMenuAsync(byte[] fileBytes, string mediaType, CancellationToken ct = default)
    {
        var systemPrompt = """
            أنت مساعد بيقرا منيو مطعم (ملف PDF أو صورة) ويطلّع كل الأصناف بقسمها وسعرها بدقة عشان تتحمّل في نظام الكاشير.
            اكتب اسم القسم واسم الصنف زي ما هم مكتوبين في المنيو من غير ما تغيّرهم أو تترجمهم.
            لو الصنف له أحجام أو أنواع بأسعار مختلفة (صغير/كبير مثلاً)، اعمل صنف منفصل لكل حجم واكتب الحجم في الاسم.
            السعر رقم بس من غير عملة. لو مفيش سعر مقروء للصنف سيبه فاضي ومتخمّنش.
            متضيفش أصناف مش موجودة في المنيو. استخدم أداة extract_menu دايمًا للرد.
            """;

        var isPdf = mediaType == "application/pdf";
        var userContent = new JsonArray
        {
            new JsonObject
            {
                ["type"] = isPdf ? "document" : "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = mediaType,
                    ["data"] = Convert.ToBase64String(fileBytes)
                }
            },
            new JsonObject { ["type"] = "text", ["text"] = "اطلع كل أصناف المنيو ده بالقسم والسعر." }
        };

        var properties = new JsonObject
        {
            ["items"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["category"] = new JsonObject { ["type"] = "string" },
                        ["name"] = new JsonObject { ["type"] = "string" },
                        ["price"] = new JsonObject { ["type"] = new JsonArray { "number", "null" } }
                    },
                    ["required"] = new JsonArray { "category", "name" }
                }
            },
            ["note"] = new JsonObject { ["type"] = new JsonArray { "string", "null" }, ["description"] = "short Arabic note if the file was unclear or is not a menu" }
        };

        var input = await CallToolAsync(systemPrompt, userContent, "extract_menu",
            "List every dish of a restaurant menu with its category and price.", properties, new JsonArray { "items" }, ct, maxTokens: 8192);

        var items = new List<MenuScanItem>();
        if (input["items"] is JsonArray arr)
        {
            foreach (var node in arr.OfType<JsonObject>())
            {
                var name = ReadString(node, "name");
                if (name is null) continue;
                items.Add(new MenuScanItem(ReadString(node, "category") ?? "منيو", name, ReadNumber(node, "price")));
            }
        }
        return new MenuScanExtraction(items, ReadString(input, "note"));
    }

    public async Task<CandidateScreeningResult> ScreenCandidateAsync(
        string jobTitle, string jobDescription, string? requirements, string resumeText, CancellationToken ct = default)
    {
        const int maxChars = 12000;
        var cv = resumeText.Length > maxChars ? resumeText[..maxChars] : resumeText;
        var systemPrompt = """
            أنت مساعد موارد بشرية بيراجع سيرة ذاتية مقابل وصف وظيفة ويدّي رأي استشاري لمسؤول التوظيف (مش قرار).
            قيّم مدى تطابق الخبرات والمهارات المكتوبة فعلًا في السيرة مع متطلبات الوظيفة، من 0 لـ 100.
            ما تعتمدش على الاسم أو الجنس أو السن أو الجنسية أو الديانة أو الحالة الاجتماعية أو الصورة، وما تذكرهمش.
            لو السيرة قصيرة أو ناقصة قول كده في الملخص ومتخمّنش خبرات مش مكتوبة.
            استخدم أداة screen_candidate دايمًا للرد.
            """;
        var userText = $"الوظيفة: {jobTitle}\n\nالوصف:\n{jobDescription}\n\nالمتطلبات:\n{requirements ?? "-"}\n\n--- السيرة الذاتية ---\n{cv}";
        var properties = new JsonObject
        {
            ["score"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 100 },
            ["summary"] = new JsonObject { ["type"] = "string", ["description"] = "two or three short sentences" },
            ["strengths"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["concerns"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
        };
        var input = await CallToolAsync(systemPrompt, JsonValue.Create(userText)!, "screen_candidate",
            "Score how well a CV fits a job description and explain briefly.", properties,
            new JsonArray { "score", "summary" }, ct);

        var score = input["score"] is { } sNode && sNode.GetValueKind() == JsonValueKind.Number ? (int)Math.Round(sNode.GetValue<decimal>()) : 0;
        static List<string> Strings(JsonNode? node) => node is JsonArray arr
            ? arr.Where(n => n is not null && n.GetValueKind() == JsonValueKind.String).Select(n => n!.GetValue<string>().Trim()).Where(x => x.Length > 0).Take(6).ToList()
            : new List<string>();
        return new CandidateScreeningResult(Math.Clamp(score, 0, 100), ReadString(input, "summary") ?? string.Empty, Strings(input["strengths"]), Strings(input["concerns"]));
    }

    private async Task<ExpenseExtractionResult> SendExtractionRequestAsync(
        string systemPrompt, JsonNode userContent, CancellationToken ct, bool includeEntryDate = false)
    {
        var properties = new JsonObject
        {
            ["amount"] = new JsonObject { ["type"] = new JsonArray { "number", "null" } },
            ["currency"] = new JsonObject { ["type"] = "string" },
            ["description"] = new JsonObject { ["type"] = "string" },
            ["suggested_account_code"] = new JsonObject { ["type"] = "string" },
            ["needs_clarification"] = new JsonObject { ["type"] = "boolean" },
            ["clarifying_question"] = new JsonObject { ["type"] = new JsonArray { "string", "null" } }
        };
        if (includeEntryDate)
            properties["entry_date"] = new JsonObject { ["type"] = new JsonArray { "string", "null" }, ["description"] = "yyyy-MM-dd, only if clearly legible on the receipt" };

        var input = await CallToolAsync(
            systemPrompt, userContent, "extract_expense",
            "Extract structured expense details from the user's message or receipt image.",
            properties, new JsonArray { "needs_clarification" }, ct);


        decimal? amount = input["amount"] is { } amountNode && amountNode.GetValueKind() != JsonValueKind.Null
            ? amountNode.GetValue<decimal>()
            : null;

        DateTime? entryDate = input["entry_date"] is { } dateNode && dateNode.GetValueKind() == JsonValueKind.String
            && DateTime.TryParse(dateNode.GetValue<string>(), out var parsedDate)
            ? parsedDate.Date
            : null;

        return new ExpenseExtractionResult(
            Amount: amount,
            Currency: input["currency"]?.GetValue<string>(),
            Description: input["description"]?.GetValue<string>(),
            SuggestedAccountCode: input["suggested_account_code"]?.GetValue<string>(),
            NeedsClarification: input["needs_clarification"]?.GetValue<bool>() ?? amount is null,
            ClarifyingQuestion: input["clarifying_question"]?.GetValue<string>(),
            EntryDate: entryDate);
    }
    private async Task<JsonObject> CallToolAsync(
        string systemPrompt, JsonNode userContent, string toolName, string toolDescription,
        JsonObject properties, JsonArray required, CancellationToken ct, int maxTokens = 2048)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new ValidationAppException(
                "المساعد الذكي مش مفعّل لسه — لازم تضيف مفتاح Claude API في إعدادات السيرفر (Claude:ApiKey) عشان يشتغل.");
        }

        await _usageLimiter.EnsureWithinDailyLimitAsync("ExpenseCapture", ct);

        var requestBody = new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = maxTokens,
            ["system"] = systemPrompt + AiLanguage.ReplyDirective(_language.PrefersArabic),
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = userContent } },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = toolName,
                    ["description"] = toolDescription,
                    ["input_schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = required
                    }
                }
            },
            ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = toolName }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new ValidationAppException($"تعذر الاتصال بمساعد الذكاء الاصطناعي (Claude API): {response.StatusCode} — {responseText}");

        var root = JsonNode.Parse(responseText)?.AsObject()
            ?? throw new ValidationAppException("رد غير متوقع من Claude API.");

        var toolUse = root["content"]?.AsArray()
            .FirstOrDefault(n => n?["type"]?.GetValue<string>() == "tool_use")
            ?? throw new ValidationAppException("لم يتمكن المساعد الذكي من فهم الرسالة، حاول تاني بصياغة أوضح.");

        return toolUse["input"]!.AsObject();
    }

    private static decimal? ReadNumber(JsonObject input, string key)
        => input[key] is { } node && node.GetValueKind() == JsonValueKind.Number ? node.GetValue<decimal>() : null;

    private static string? ReadString(JsonObject input, string key)
        => input[key] is { } node && node.GetValueKind() == JsonValueKind.String && !string.IsNullOrWhiteSpace(node.GetValue<string>())
            ? node.GetValue<string>().Trim()
            : null;
}
