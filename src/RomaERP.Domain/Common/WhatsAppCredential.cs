namespace RomaERP.Domain.Common;

/// <summary>Per-tenant WhatsApp Business Cloud API settings for sending Alerts-system notifications.
/// Kept in the tenant's own database (database-per-tenant), so one tenant's WhatsApp credentials can
/// never be used to send messages on behalf of another tenant. The access token is encrypted at rest
/// via ISecretProtector, matching the pattern already used for e-invoicing secrets.</summary>
public class WhatsAppCredential : AuditableEntity
{
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessTokenEncrypted { get; set; } = string.Empty;
    public string RecipientPhoneNumber { get; set; } = string.Empty;

    // WhatsApp's Cloud API only allows a business to message a number outside an active 24-hour
    // customer-service window using a pre-approved message template — plain free-text is rejected.
    // The tenant creates one simple template (one body placeholder) in Meta Business Manager and
    // tells us its name/language here.
    public string TemplateName { get; set; } = "romaerp_alert";
    public string TemplateLanguageCode { get; set; } = "ar";

    public bool IsEnabled { get; set; }
}
