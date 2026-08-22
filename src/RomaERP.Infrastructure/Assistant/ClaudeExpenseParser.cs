using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;

namespace RomaERP.Infrastructure.Assistant;

public class ClaudeExpenseParser : IClaudeExpenseParser
{
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly ClaudeSettings _settings;

    public ClaudeExpenseParser(HttpClient httpClient, IOptions<ClaudeSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<ExpenseExtractionResult> ExtractAsync(
        string userMessage,
        string? priorContext,
        IReadOnlyList<ExpenseAccountCandidate> expenseAccounts,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new ValidationAppException(
                "المساعد الذكي مش مفعّل لسه — لازم تضيف مفتاح Claude API في إعدادات السيرفر (Claude:ApiKey) عشان يشتغل.");
        }

        var accountsList = string.Join("\n", expenseAccounts.Select(a => $"- {a.Code}: {a.NameAr}"));
        var systemPrompt = $"""
            أنت مساعد محاسبي بيساعد موظف يسجّل مصروفات الشركة عن طريق الكلام العادي (مصري أو عربي أو إنجليزي).
            مهمتك تستخرج من رسالة المستخدم: المبلغ، وصف قصير للمصروف، وأنسب حساب مصروفات من القائمة دي:
            {accountsList}

            لو المبلغ مش واضح أو مذكورش، اطلب توضيح بسؤال عربي قصير ومباشر.
            استخدم أداة extract_expense دايمًا للرد.
            """;

        var messageContent = priorContext is null ? userMessage : $"(الرسالة السابقة: {priorContext})\n{userMessage}";

        var requestBody = new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = 1024,
            ["system"] = systemPrompt,
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = messageContent } },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "extract_expense",
                    ["description"] = "Extract structured expense details from the user's message.",
                    ["input_schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["amount"] = new JsonObject { ["type"] = new JsonArray { "number", "null" } },
                            ["currency"] = new JsonObject { ["type"] = "string" },
                            ["description"] = new JsonObject { ["type"] = "string" },
                            ["suggested_account_code"] = new JsonObject { ["type"] = "string" },
                            ["needs_clarification"] = new JsonObject { ["type"] = "boolean" },
                            ["clarifying_question"] = new JsonObject { ["type"] = new JsonArray { "string", "null" } }
                        },
                        ["required"] = new JsonArray { "needs_clarification" }
                    }
                }
            },
            ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = "extract_expense" }
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

        var input = toolUse["input"]!.AsObject();

        decimal? amount = input["amount"] is { } amountNode && amountNode.GetValueKind() != JsonValueKind.Null
            ? amountNode.GetValue<decimal>()
            : null;

        return new ExpenseExtractionResult(
            Amount: amount,
            Currency: input["currency"]?.GetValue<string>(),
            Description: input["description"]?.GetValue<string>(),
            SuggestedAccountCode: input["suggested_account_code"]?.GetValue<string>(),
            NeedsClarification: input["needs_clarification"]?.GetValue<bool>() ?? amount is null,
            ClarifyingQuestion: input["clarifying_question"]?.GetValue<string>());
    }
}
