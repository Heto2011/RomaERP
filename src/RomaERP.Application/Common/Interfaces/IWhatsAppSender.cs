namespace RomaERP.Application.Common.Interfaces;

public record WhatsAppSendResult(bool Success, string? FailureReason);

/// <summary>Sends one WhatsApp template message via Meta's WhatsApp Business Cloud API. Stateless — the
/// tenant's own phone-number-id/access-token/recipient are supplied per call (looked up from that tenant's
/// own WhatsAppCredential row), so one tenant's messages can never be sent using another tenant's account.
/// WhatsApp only allows messaging a number outside an active 24-hour customer-service window using a
/// pre-approved template, so every send goes through a template (not free-text).</summary>
public interface IWhatsAppSender
{
    Task<WhatsAppSendResult> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhoneNumber,
        string templateName,
        string templateLanguageCode,
        string bodyText,
        CancellationToken ct = default);
}
