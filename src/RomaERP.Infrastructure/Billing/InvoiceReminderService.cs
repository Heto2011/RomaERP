using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Billing;

/// <summary>Emails the company's contact about an unpaid subscription invoice: a reminder on the due date, a warning after a
/// week and a final notice after two weeks. It only writes to the customer, it never suspends anyone (that stays the owner's call),
/// and each stage is sent once per invoice, so running it often is safe. Email is free to send, unlike a WhatsApp message.</summary>
public class InvoiceReminderService
{
    private readonly CentralDbContext _central;
    private readonly IEmailSender _email;
    private readonly ILogger<InvoiceReminderService> _logger;

    public InvoiceReminderService(CentralDbContext central, IEmailSender email, ILogger<InvoiceReminderService> logger)
    {
        _central = central;
        _email = email;
        _logger = logger;
    }

    /// <summary>Which reminder an invoice is due for: 0 none yet, 1 on the due date, 2 a week late, 3 two weeks late.</summary>
    public static int StageFor(DateTime dueDateUtc, DateTime nowUtc)
    {
        var late = nowUtc - dueDateUtc;
        if (late < TimeSpan.Zero) return 0;
        return late.TotalDays >= 14 ? 3 : late.TotalDays >= 7 ? 2 : 1;
    }

    public async Task<int> SendDueRemindersAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        if (!_email.IsConfigured) return 0;

        var open = await _central.SubscriptionInvoices
            .Where(i => (i.Status == SubscriptionInvoiceStatus.Pending || i.Status == SubscriptionInvoiceStatus.Failed)
                        && i.TotalAmount > 0 && i.DueDateUtc <= nowUtc && i.ReminderStage < 3)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var invoice in open)
        {
            var stage = StageFor(invoice.DueDateUtc, nowUtc);
            if (stage <= invoice.ReminderStage) continue;

            var tenant = await _central.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invoice.TenantId, ct);
            if (tenant is null || tenant.IsDemo || string.IsNullOrWhiteSpace(tenant.ContactEmail)) continue;

            var (subject, body) = Compose(stage, tenant.CompanyNameAr, tenant.CompanyNameEn, invoice);
            var result = await _email.SendAsync(tenant.ContactEmail.Trim(), subject, body, ct);
            if (!result.Success)
            {
                _logger.LogWarning("Invoice reminder to {Company} failed: {Reason}", tenant.CompanyCode, result.FailureReason);
                continue; // not marked as sent, so the next run tries again
            }

            invoice.ReminderStage = stage;
            _central.TenantActivities.Add(RomaERP.Infrastructure.Tenancy.TenantActivityLog.Build(
                tenant.Id, tenant.CompanyCode, "Billing", $"Payment reminder {stage} emailed", $"{invoice.TotalAmount} {invoice.Currency}", "automatic"));
            await _central.SaveChangesAsync(ct);
            sent++;
        }
        return sent;
    }

    private static (string Subject, string Body) Compose(int stage, string nameAr, string nameEn, SubscriptionInvoice invoice)
    {
        var amount = $"{invoice.TotalAmount:0.##} {invoice.Currency}";
        var due = invoice.DueDateUtc.ToString("yyyy-MM-dd");
        var company = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(nameAr) ? nameEn : nameAr);
        var companyEn = WebUtility.HtmlEncode(nameEn);

        var (subject, lineAr, lineEn) = stage switch
        {
            1 => ("Roma: your subscription invoice is due / فاتورة اشتراكك مستحقة",
                  $"فاتورة اشتراك روما بقيمة {amount} مستحقة السداد في {due}. لو تم الدفع بالفعل، تجاهل الرسالة وشكرًا لك.",
                  $"Your Roma subscription invoice of {amount} was due on {due}. If you have already paid, please ignore this message."),
            2 => ("Roma: reminder, your invoice is a week overdue / تذكير: فاتورتك متأخرة أسبوع",
                  $"لسه فاتورة اشتراك روما بقيمة {amount} (استحقت في {due}) مش مسددة. من فضلك سدّدها، أو كلمنا لو في أي صعوبة ونرتب معاك.",
                  $"Your Roma invoice of {amount} (due {due}) is still unpaid. Please pay it, or contact us if there is any difficulty and we will help."),
            _ => ("Roma: final notice before your account is paused / إشعار أخير قبل إيقاف الحساب مؤقتًا",
                  $"فاتورة اشتراك روما بقيمة {amount} (استحقت في {due}) متأخرة أسبوعين. لو ما اتسددتش، ممكن يتوقف حسابك مؤقتًا. بياناتك محفوظة ومش بتتمسح، والحساب يرجع فور السداد. كلمنا لو محتاج مهلة.",
                  $"Your Roma invoice of {amount} (due {due}) is two weeks overdue. If it stays unpaid your account may be paused. Your data is kept safe and nothing is deleted, and the account returns as soon as you pay. Contact us if you need more time."),
        };

        var body = $"<div style=\"font-family:Arial,sans-serif;line-height:1.7\"><p dir=\"rtl\">مرحبًا {company}،<br/>{lineAr}</p><hr/><p dir=\"ltr\">Hello {companyEn},<br/>{lineEn}</p><p>Roma</p></div>";
        return (subject, body);
    }
}
