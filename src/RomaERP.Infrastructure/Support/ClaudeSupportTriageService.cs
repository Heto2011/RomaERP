using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RomaERP.Application.Support.Services;
using RomaERP.Infrastructure.Assistant;

namespace RomaERP.Infrastructure.Support;

/// <summary>Gives every new support ticket an immediate first look from Claude before a human ever sees
/// it — answers generic product questions on the spot, and leaves anything account-specific, billing, or
/// bug-shaped for a human rather than guessing. Disabled gracefully (always escalates) if no Claude API
/// key is configured, same as the expense assistant.</summary>
public class ClaudeSupportTriageService : ISupportAiTriageService
{
    private const string AnthropicVersion = "2023-06-01";

    private static readonly string ProductKnowledge = """
        RomaERP (ROMA) نظام إدارة أعمال متكامل للمطاعم والشركات في السعودية ومصر ودول الخليج، بيشمل:
        المحاسبة والقيود اليومية، نقاط البيع (POS) للمطاعم، المخزون والتكاليف، المبيعات والمشتريات،
        الموارد البشرية والرواتب والحضور، التقارير المالية، والذكاء المالي والتنبؤ.
        فترة تجربة مجانية 21 يوم من غير بطاقة بنكية. النظام متاح كتطبيق ويب وكـ PWA يتقدر يتثبّت على الموبايل
        (Add to Home Screen) زي أي تطبيق عادي. الدعم الفني الأساسي عن طريق نظام التذاكر، والواتساب قناة مساندة إضافية.
        """;

    private readonly HttpClient _httpClient;
    private readonly ClaudeSettings _settings;

    public ClaudeSupportTriageService(HttpClient httpClient, IOptions<ClaudeSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<SupportTriageResult> TriageAsync(string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return new SupportTriageResult(false, null, "المساعد الذكي مش مفعّل — التذكرة محتاجة مراجعة بشرية.");

        var systemPrompt = $"""
            أنت أول خط رد على تذاكر الدعم الفني لنظام RomaERP. معلوماتك عن المنتج:
            {ProductKnowledge}

            لو السؤال عام وتقدر تجاوب عليه بثقة من غير ما تحتاج توصل لبيانات حساب العميل تحديدًا
            (زي "إيه المميزات المتاحة؟" أو "فترة التجربة قد إيه؟")، جاوب إجابة مفيدة وقصيرة بالعربي.
            لو السؤال يحتاج الوصول لبيانات حساب العميل، أو مشكلة تقنية/باغ، أو موضوع فواتير ودفع،
            أو أي حاجة مش متأكد منها 100%، سيبها لفريق الدعم البشري ومتخمنش.
            استخدم أداة triage_ticket دايمًا للرد.
            """;

        var requestBody = new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = 1024,
            ["system"] = systemPrompt,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = $"الموضوع: {subject}\n\nالرسالة: {body}" }
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "triage_ticket",
                    ["description"] = "Decide whether this support ticket can be answered automatically or needs a human.",
                    ["input_schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["can_answer"] = new JsonObject { ["type"] = "boolean" },
                            ["answer"] = new JsonObject { ["type"] = new JsonArray { "string", "null" } },
                            ["escalation_reason"] = new JsonObject { ["type"] = new JsonArray { "string", "null" } }
                        },
                        ["required"] = new JsonArray { "can_answer" }
                    }
                }
            },
            ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = "triage_ticket" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return new SupportTriageResult(false, null, "تعذر الوصول للمساعد الذكي — التذكرة محتاجة مراجعة بشرية.");

            var root = JsonNode.Parse(responseText)?.AsObject();
            var toolUse = root?["content"]?.AsArray().FirstOrDefault(n => n?["type"]?.GetValue<string>() == "tool_use");
            if (toolUse is null)
                return new SupportTriageResult(false, null, "لم يتمكن المساعد الذكي من تحليل التذكرة.");

            var input = toolUse["input"]!.AsObject();
            var canAnswer = input["can_answer"]?.GetValue<bool>() ?? false;
            var answer = input["answer"]?.GetValueKind() == JsonValueKind.String ? input["answer"]!.GetValue<string>() : null;
            var reason = input["escalation_reason"]?.GetValueKind() == JsonValueKind.String ? input["escalation_reason"]!.GetValue<string>() : null;

            return canAnswer && !string.IsNullOrWhiteSpace(answer)
                ? new SupportTriageResult(true, answer, null)
                : new SupportTriageResult(false, null, reason ?? "التذكرة محتاجة مراجعة بشرية.");
        }
        catch (Exception)
        {
            // Best-effort — a triage failure must never block ticket creation. Falls back to human review.
            return new SupportTriageResult(false, null, "تعذر الوصول للمساعد الذكي — التذكرة محتاجة مراجعة بشرية.");
        }
    }
}
