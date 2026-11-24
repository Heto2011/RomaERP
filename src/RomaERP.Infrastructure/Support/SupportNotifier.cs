using System.Net;
using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Support.Services;

namespace RomaERP.Infrastructure.Support;

/// <summary>Best-effort emails around support tickets: a failure here must never stop a ticket or a reply from being saved.</summary>
public class SupportNotifier : ISupportNotifier
{
    public const string DefaultOwnerAddress = "support@romagroup.app";
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    public SupportNotifier(IEmailSender email, IConfiguration config) { _email = email; _config = config; }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    public async Task NotifyOwnerAsync(int ticketNumber, string companyCode, string requesterName, string subject, string message, bool isNew, bool aiAnswered, CancellationToken ct = default)
    {
        if (!_email.IsConfigured) return;
        var to = _config["Support:NotifyEmail"];
        if (string.IsNullOrWhiteSpace(to)) to = DefaultOwnerAddress;
        var head = isNew ? "تذكرة دعم جديدة" : "رد جديد من عميل";
        var body = $"""
            <div dir="rtl" style="font-family:sans-serif">
              <h3>{head} #{ticketNumber}</h3>
              <p>الشركة: <b>{Esc(companyCode)}</b> — {Esc(requesterName)}</p>
              <p>الموضوع: {Esc(subject)}</p>
              <blockquote style="border-inline-start:3px solid #999;margin:0;padding:0 10px;white-space:pre-wrap">{Esc(Cut(message, 600))}</blockquote>
              <p>{(aiAnswered ? "المساعد الذكي رد عليها وهي منتظرة العميل." : "محتاجة ردك من صفحة تذاكر الدعم في الكونسول.")}</p>
            </div>
            """;
        try { await _email.SendAsync(to!, $"{head} #{ticketNumber} — {Cut(subject, 60)}", body, ct); } catch { /* never block the ticket */ }
    }

    public async Task NotifyCustomerAsync(int ticketNumber, string requesterEmail, string subject, string reply, CancellationToken ct = default)
    {
        if (!_email.IsConfigured || string.IsNullOrWhiteSpace(requesterEmail)) return;
        var body = $"""
            <div dir="rtl" style="font-family:sans-serif">
              <h3>فريق الدعم رد على تذكرتك #{ticketNumber}</h3>
              <p>الموضوع: {Esc(subject)}</p>
              <blockquote style="border-inline-start:3px solid #999;margin:0;padding:0 10px;white-space:pre-wrap">{Esc(Cut(reply, 1500))}</blockquote>
              <p>تقدر تكمل الرد من صفحة "الدعم" جوه النظام.</p>
            </div>
            """;
        try { await _email.SendAsync(requesterEmail, $"رد على تذكرتك #{ticketNumber}", body, ct); } catch { /* never block the reply */ }
    }
}
