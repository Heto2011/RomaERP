using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RomaERP.Application.Assistant.Agent;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Assistant;

/// <summary>Sends one round of the conversation (with the tool definitions) to Claude and returns its raw answer. The usage allowance is checked once per user request by the agent service.</summary>
public class ClaudeAgentClient : IAgentModelClient
{
    private const string AnthropicVersion = "2023-06-01";
    private readonly HttpClient _http;
    private readonly ClaudeSettings _settings;
    private readonly IUserLanguage _language;

    public ClaudeAgentClient(HttpClient http, IOptions<ClaudeSettings> settings, IUserLanguage language) { _http = http; _settings = settings.Value; _language = language; }

    public async Task<JsonObject> CompleteAsync(string system, JsonArray messages, JsonArray tools, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new ValidationAppException("المساعد الذكي مش مفعّل لسه — لازم تضيف مفتاح Claude API في إعدادات السيرفر (Claude:ApiKey) عشان يشتغل.");
        var body = new JsonObject
        {
            ["model"] = _settings.Model, ["max_tokens"] = 1500,
            ["system"] = system + AiLanguage.ReplyDirective(_language.PrefersArabic),
            ["messages"] = JsonNode.Parse(messages.ToJsonString()),
            ["tools"] = JsonNode.Parse(tools.ToJsonString()),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new ValidationAppException($"تعذر الاتصال بمساعد الذكاء الاصطناعي (Claude API): {response.StatusCode}");
        return JsonNode.Parse(text)?.AsObject() ?? throw new ValidationAppException("رد غير متوقع من Claude API.");
    }
}
