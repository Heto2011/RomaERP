using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Alerts.DTOs;
using RomaERP.Application.Alerts.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Notifications.DTOs;
using RomaERP.Domain.Common;

namespace RomaERP.Application.Notifications.Services;

/// <summary>Per-tenant WhatsApp notification settings and sending, following the same "inert until
/// configured" shape as PayTabs/GOSI/delivery platforms — every method degrades to a clear Arabic message
/// instead of throwing when the tenant hasn't set up their WhatsApp Business Cloud API credentials yet.</summary>
public class WhatsAppNotificationService : IWhatsAppNotificationService
{
    private const int MaxDigestLines = 10;

    private readonly IApplicationDbContext _context;
    private readonly ISecretProtector _secretProtector;
    private readonly IWhatsAppSender _sender;
    private readonly IAlertsService _alertsService;

    public WhatsAppNotificationService(
        IApplicationDbContext context,
        ISecretProtector secretProtector,
        IWhatsAppSender sender,
        IAlertsService alertsService)
    {
        _context = context;
        _secretProtector = secretProtector;
        _sender = sender;
        _alertsService = alertsService;
    }

    public async Task<WhatsAppStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var credential = await _context.WhatsAppCredentials.FirstOrDefaultAsync(ct);
        return Map(credential);
    }

    public async Task<WhatsAppStatusDto> SaveCredentialAsync(SaveWhatsAppCredentialDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.PhoneNumberId) || string.IsNullOrWhiteSpace(dto.RecipientPhoneNumber))
            throw new ValidationAppException("رقم الواتساب بزنس (Phone Number ID) ورقم الاستقبال مطلوبين.");
        if (string.IsNullOrWhiteSpace(dto.TemplateName) || string.IsNullOrWhiteSpace(dto.TemplateLanguageCode))
            throw new ValidationAppException("اسم القالب (Template) ولغته مطلوبين.");

        var credential = await _context.WhatsAppCredentials.FirstOrDefaultAsync(ct);
        if (credential is null)
        {
            if (string.IsNullOrWhiteSpace(dto.AccessToken))
                throw new ValidationAppException("الـ Access Token مطلوب أول مرة تظبط فيها الإعداد.");

            credential = new WhatsAppCredential();
            _context.WhatsAppCredentials.Add(credential);
        }

        credential.PhoneNumberId = dto.PhoneNumberId.Trim();
        credential.RecipientPhoneNumber = dto.RecipientPhoneNumber.Trim();
        credential.TemplateName = dto.TemplateName.Trim();
        credential.TemplateLanguageCode = dto.TemplateLanguageCode.Trim();
        credential.IsEnabled = dto.IsEnabled;
        if (!string.IsNullOrWhiteSpace(dto.AccessToken))
            credential.AccessTokenEncrypted = _secretProtector.Protect(dto.AccessToken.Trim());

        await _context.SaveChangesAsync(ct);
        return Map(credential);
    }

    public async Task<WhatsAppSendResultDto> SendTestMessageAsync(CancellationToken ct = default)
    {
        var credential = await _context.WhatsAppCredentials.FirstOrDefaultAsync(ct);
        if (!IsUsable(credential))
            return NotConfiguredResult();

        var result = await SendAsync(credential!, "رسالة تجربة من روما إي آر بي — لو وصلتك دي، الإعداد شغال تمام.", ct);
        return new WhatsAppSendResultDto { Success = result.Success, FailureReason = result.FailureReason };
    }

    public async Task<WhatsAppSendResultDto> SendAlertsDigestAsync(bool skipIfNothingSignificant, CancellationToken ct = default)
    {
        var credential = await _context.WhatsAppCredentials.FirstOrDefaultAsync(ct);
        if (!IsUsable(credential) || !credential!.IsEnabled)
            return NotConfiguredResult();

        var report = await _alertsService.GetAlertsAsync(ct);
        var significant = report.Alerts
            .Where(a => a.Severity >= AlertSeverity.Warning)
            .OrderByDescending(a => a.Severity)
            .Take(MaxDigestLines)
            .ToList();

        if (significant.Count == 0)
        {
            if (skipIfNothingSignificant)
                return new WhatsAppSendResultDto { Success = true, FailureReason = null };

            var allClear = await SendAsync(credential, "روما إي آر بي: مفيش تنبيهات مهمة دلوقتي، كل حاجة تمام.", ct);
            return new WhatsAppSendResultDto { Success = allClear.Success, FailureReason = allClear.FailureReason };
        }

        var body = BuildDigestText(significant);
        var sendResult = await SendAsync(credential, body, ct);
        return new WhatsAppSendResultDto { Success = sendResult.Success, FailureReason = sendResult.FailureReason };
    }

    private Task<WhatsAppSendResult> SendAsync(WhatsAppCredential credential, string bodyText, CancellationToken ct) =>
        _sender.SendTemplateMessageAsync(
            credential.PhoneNumberId,
            _secretProtector.Unprotect(credential.AccessTokenEncrypted),
            credential.RecipientPhoneNumber,
            credential.TemplateName,
            credential.TemplateLanguageCode,
            bodyText,
            ct);

    private static bool IsUsable(WhatsAppCredential? credential) =>
        credential is not null
        && !string.IsNullOrWhiteSpace(credential.PhoneNumberId)
        && !string.IsNullOrWhiteSpace(credential.AccessTokenEncrypted)
        && !string.IsNullOrWhiteSpace(credential.RecipientPhoneNumber);

    private static WhatsAppSendResultDto NotConfiguredResult() => new()
    {
        Success = false,
        FailureReason = "الواتساب لسه مش متظبط أو مش مفعّل — روح لإعدادات الواتساب وسجّل بيانات حسابك الأول."
    };

    private static string BuildDigestText(List<AlertDto> alerts)
    {
        var lines = alerts.Select(a => $"{SeverityLabel(a.Severity)} {a.Title}: {a.Detail}");
        return "تنبيهات روما إي آر بي:\n" + string.Join("\n", lines);
    }

    private static string SeverityLabel(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "[حرج]",
        AlertSeverity.Warning => "[تنبيه]",
        _ => "[معلومة]"
    };

    private static WhatsAppStatusDto Map(WhatsAppCredential? credential) => new()
    {
        IsConfigured = IsUsable(credential),
        IsEnabled = credential?.IsEnabled ?? false,
        PhoneNumberId = credential?.PhoneNumberId,
        RecipientPhoneNumber = credential?.RecipientPhoneNumber,
        TemplateName = credential?.TemplateName ?? "romaerp_alert",
        TemplateLanguageCode = credential?.TemplateLanguageCode ?? "ar"
    };
}
