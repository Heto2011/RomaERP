using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Notifications;

/// <summary>Sends one WhatsApp template message per call through Meta's WhatsApp Business Cloud API
/// (graph.facebook.com). Credentials are per-tenant (see WhatsAppNotificationService), so this class holds
/// no state of its own beyond the HttpClient.</summary>
public class MetaWhatsAppSender : IWhatsAppSender
{
    private const string GraphApiVersion = "v20.0";

    private readonly HttpClient _http;

    public MetaWhatsAppSender(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://graph.facebook.com/");
    }

    public async Task<WhatsAppSendResult> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhoneNumber,
        string templateName,
        string templateLanguageCode,
        string bodyText,
        CancellationToken ct = default)
    {
        var payload = new WhatsAppMessageRequest(
            MessagingProduct: "whatsapp",
            To: recipientPhoneNumber,
            Type: "template",
            Template: new WhatsAppTemplate(
                Name: templateName,
                Language: new WhatsAppTemplateLanguage(templateLanguageCode),
                Components: new[]
                {
                    new WhatsAppTemplateComponent("body", new[] { new WhatsAppTemplateParameter("text", bodyText) })
                }));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphApiVersion}/{phoneNumberId}/messages")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return new WhatsAppSendResult(true, null);

            var error = await response.Content.ReadFromJsonAsync<WhatsAppErrorResponse>(cancellationToken: ct);
            return new WhatsAppSendResult(false, error?.Error?.Message ?? $"WhatsApp API returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new WhatsAppSendResult(false, ex.Message);
        }
    }

    private record WhatsAppMessageRequest(
        [property: JsonPropertyName("messaging_product")] string MessagingProduct,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("template")] WhatsAppTemplate Template);

    private record WhatsAppTemplate(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("language")] WhatsAppTemplateLanguage Language,
        [property: JsonPropertyName("components")] WhatsAppTemplateComponent[] Components);

    private record WhatsAppTemplateLanguage([property: JsonPropertyName("code")] string Code);

    private record WhatsAppTemplateComponent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("parameters")] WhatsAppTemplateParameter[] Parameters);

    private record WhatsAppTemplateParameter(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private record WhatsAppErrorResponse([property: JsonPropertyName("error")] WhatsAppError? Error);

    private record WhatsAppError([property: JsonPropertyName("message")] string? Message);
}
