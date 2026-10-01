using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.Identity;

public class PasswordRecoveryService : IPasswordRecoveryService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _email;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordRecoveryService> _logger;

    public PasswordRecoveryService(UserManager<ApplicationUser> userManager, IEmailSender email, ITenantContext tenant,
        IConfiguration configuration, ILogger<PasswordRecoveryService> logger)
    {
        _userManager = userManager;
        _email = email;
        _tenant = tenant;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RequestResetAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return;

        if (!_email.IsConfigured)
        {
            _logger.LogWarning("Password reset requested for {Email} but no email provider is configured.", user.Email);
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var baseUrl = (_configuration["App:PublicBaseUrl"] ?? "https://romagroup.app").TrimEnd('/');
        var portal = _tenant.ProductScope == ProductScope.PeopleOnly ? "&p=people" : "";
        var link = $"{baseUrl}/reset-password?c={Uri.EscapeDataString(_tenant.CompanyCode)}&e={Uri.EscapeDataString(user.Email)}&t={Uri.EscapeDataString(token)}{portal}";

        var result = await _email.SendAsync(user.Email, "إعادة تعيين كلمة السر | Reset your password", BuildHtml(user.FullName, link), ct);
        if (!result.Success)
            _logger.LogError("Password reset email to {Email} failed: {Reason}", user.Email, result.FailureReason);
    }

    public async Task<string?> ResetAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        const string invalid = "الرابط غير صالح أو منتهي. اطلب رابط جديد.";
        if (user is null || !user.IsActive) return invalid;

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded) return null;
        return result.Errors.Any(e => e.Code == "InvalidToken") ? invalid : string.Join("، ", result.Errors.Select(e => e.Description));
    }

    private static string BuildHtml(string name, string link)
    {
        var n = WebUtility.HtmlEncode(name);
        var l = WebUtility.HtmlEncode(link);
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:auto;padding:24px;color:#1d2433">
              <div dir="rtl" style="text-align:right">
                <h2 style="margin:0 0 12px">أهلاً {n}</h2>
                <p>وصلنا طلب لإعادة تعيين كلمة السر لحسابك. اضغط على الزرار لاختيار كلمة سر جديدة. الرابط صالح لفترة قصيرة ولمرة واحدة.</p>
                <p><a href="{l}" style="display:inline-block;background:#1f6f5c;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:bold">تغيير كلمة السر</a></p>
                <p style="color:#6b7280;font-size:13px">لو ماطلبتش ده، تجاهل الرسالة وحسابك آمن.</p>
              </div>
              <hr style="border:none;border-top:1px solid #e5e7eb;margin:20px 0">
              <div dir="ltr" style="text-align:left">
                <h3 style="margin:0 0 8px">Hi {n}</h3>
                <p>We received a request to reset your password. Use the button below to choose a new one. The link works once and expires soon.</p>
                <p><a href="{l}" style="display:inline-block;background:#1f6f5c;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:bold">Reset password</a></p>
                <p style="color:#6b7280;font-size:13px">If you didn't ask for this, ignore this email — your account is safe.</p>
              </div>
            </div>
            """;
    }
}
