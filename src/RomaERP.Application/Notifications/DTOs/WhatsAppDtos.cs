namespace RomaERP.Application.Notifications.DTOs;

public class WhatsAppStatusDto
{
    public bool IsConfigured { get; set; }
    public bool IsEnabled { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? RecipientPhoneNumber { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateLanguageCode { get; set; } = string.Empty;
}

public class SaveWhatsAppCredentialDto
{
    public string PhoneNumberId { get; set; } = string.Empty;

    // Null keeps the previously saved token (matches the e-invoicing secret pattern) — required only
    // the first time a tenant sets this up.
    public string? AccessToken { get; set; }
    public string RecipientPhoneNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = "romaerp_alert";
    public string TemplateLanguageCode { get; set; } = "ar";
    public bool IsEnabled { get; set; }
}

public class WhatsAppSendResultDto
{
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}
