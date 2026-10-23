using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Notifications.Services;
using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.Services;

public record ReminderOutcome(int Prepared, int PendingApproval, bool WhatsAppSent, int EmailsSent);

public interface IRecurringExpenseReminderService
{
    /// <summary>Prepares this company's fixed monthly expenses that have come due and, only when something new was prepared,
    /// tells the Admins (WhatsApp if the company enabled it, email if a provider is configured). Safe to run every day:
    /// a month's expense is prepared once, so each notice goes out once.</summary>
    Task<ReminderOutcome> RunAsync(DateTime today, bool arabic, string baseUrl, CancellationToken ct = default);
}

public class RecurringExpenseReminderService : IRecurringExpenseReminderService
{
    private readonly IRecurringExpenseService _recurring;
    private readonly IApplicationDbContext _context;
    private readonly IWhatsAppNotificationService _whatsApp;
    private readonly IEmailSender _email;
    private readonly IAdminEmailProvider _admins;
    private readonly ILogger<RecurringExpenseReminderService> _logger;

    public RecurringExpenseReminderService(IRecurringExpenseService recurring, IApplicationDbContext context, IWhatsAppNotificationService whatsApp,
        IEmailSender email, IAdminEmailProvider admins, ILogger<RecurringExpenseReminderService> logger)
    {
        _recurring = recurring;
        _context = context;
        _whatsApp = whatsApp;
        _email = email;
        _admins = admins;
        _logger = logger;
    }

    public async Task<ReminderOutcome> RunAsync(DateTime today, bool arabic, string baseUrl, CancellationToken ct = default)
    {
        var prepared = await _recurring.PrepareDueAsync(today, "system", ct);
        if (prepared == 0) return new ReminderOutcome(0, 0, false, 0);

        var pending = await _context.ExpenseCaptures.CountAsync(c => c.Status == ExpenseCaptureStatus.PendingApproval && !c.IsDeleted, ct);
        var link = $"{baseUrl.TrimEnd('/')}/assistant/approvals";
        var (whatsAppText, subject, html) = BuildMessages(arabic, prepared, pending, link);

        var whatsAppSent = false;
        try
        {
            whatsAppSent = (await _whatsApp.SendTextAsync(whatsAppText, ct)).Success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp reminder for fixed expenses failed.");
        }

        var emails = 0;
        if (_email.IsConfigured)
        {
            foreach (var admin in await _admins.GetAdminContactsAsync(ct))
            {
                var result = await _email.SendAsync(admin.Email, subject, html.Replace("__NAME__", WebUtility.HtmlEncode(admin.Name)), ct);
                if (result.Success) emails++;
                else _logger.LogWarning("Fixed-expense reminder email to {Email} failed: {Reason}", admin.Email, result.FailureReason);
            }
        }
        return new ReminderOutcome(prepared, pending, whatsAppSent, emails);
    }

    /// <summary>The three texts of one reminder (pure, so it is unit-tested).</summary>
    public static (string WhatsApp, string Subject, string Html) BuildMessages(bool arabic, int prepared, int pending, string link)
    {
        if (arabic)
        {
            var whatsApp = $"روما إي آر بي: جهّزنا {prepared} مصروف ثابت لهذا الشهر، وإجمالي المصروفات في انتظار اعتمادك {pending}. راجعها من: {link}";
            var html = $"""
                <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif;font-size:16px;line-height:1.7;color:#1f2937">
                  <p>أهلًا __NAME__،</p>
                  <p>جهّزنا <b>{prepared}</b> مصروف ثابت لهذا الشهر (إيجار، كهرباء…)، وإجمالي المصروفات في انتظار اعتمادك الآن <b>{pending}</b>.</p>
                  <p>راجع المبالغ وعدّلها لو لزم، ثم اعتمد: <a href="{link}">فتح شاشة الاعتماد</a></p>
                  <p style="color:#6b7280;font-size:13px">لن يُسجَّل أي قيد قبل موافقتك. Roma ERP</p>
                </div>
                """;
            return (whatsApp, "مصروفات ثابتة تنتظر اعتمادك | Fixed expenses awaiting approval", html);
        }
        var whatsAppEn = $"Roma ERP: {prepared} fixed monthly expense(s) were prepared for this month; {pending} expense(s) in total are waiting for your approval. Review: {link}";
        var htmlEn = $"""
            <div style="font-family:Arial,sans-serif;font-size:16px;line-height:1.7;color:#1f2937">
              <p>Hello __NAME__,</p>
              <p>We prepared <b>{prepared}</b> fixed monthly expense(s) for this month (rent, electricity…); <b>{pending}</b> expense(s) in total are now waiting for your approval.</p>
              <p>Review the amounts, change them if needed, then approve: <a href="{link}">Open the approvals screen</a></p>
              <p style="color:#6b7280;font-size:13px">Nothing is posted before you approve. Roma ERP</p>
            </div>
            """;
        return (whatsAppEn, "Fixed expenses awaiting approval | مصروفات ثابتة تنتظر اعتمادك", htmlEn);
    }
}
