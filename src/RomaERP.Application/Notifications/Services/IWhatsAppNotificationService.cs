using RomaERP.Application.Notifications.DTOs;

namespace RomaERP.Application.Notifications.Services;

public interface IWhatsAppNotificationService
{
    Task<WhatsAppStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<WhatsAppStatusDto> SaveCredentialAsync(SaveWhatsAppCredentialDto dto, CancellationToken ct = default);
    Task<WhatsAppSendResultDto> SendTestMessageAsync(CancellationToken ct = default);

    /// <summary>Sends a digest of the current Warning/Critical alerts. <paramref name="skipIfNothingSignificant"/>
    /// is true for the unattended daily job (no point paying for a template message that just says "all clear"
    /// every day) and false for the user-triggered "Send via WhatsApp" button (an explicit ask deserves a reply
    /// either way).</summary>
    Task<WhatsAppSendResultDto> SendAlertsDigestAsync(bool skipIfNothingSignificant, CancellationToken ct = default);
}
