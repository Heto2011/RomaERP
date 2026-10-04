using System.Globalization;
using System.Net;
using System.Text;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common;

/// <summary>Printable HTML for Roma Group's own subscription invoice (what a customer owes Roma) — NOT the
/// invoices a customer issues to their own clients, which carry that customer's name. Rendered to PDF through
/// IHtmlToPdfRenderer. Every dynamic string is HTML-encoded: the document is rendered by a real browser on the server.</summary>
public static class SubscriptionInvoiceHtmlTemplate
{
    /// <summary>Stable, human-readable invoice number derived from the period and the invoice id.</summary>
    public static string NumberFor(SubscriptionInvoiceDto invoice)
        => $"RG-{invoice.PeriodStart:yyyyMM}-{invoice.Id.ToString("N")[..6].ToUpperInvariant()}";

    public static string Build(SubscriptionInvoiceDto invoice, string? logoPngBase64, bool arabic)
    {
        var culture = CultureInfo.InvariantCulture;
        string Enc(string? v) => WebUtility.HtmlEncode(v ?? string.Empty);
        string L(string ar, string en) => arabic ? ar : en;
        string Money(decimal amount) => amount.ToString("N2", culture);
        string Date(DateTime d) => d.ToString("yyyy-MM-dd", culture);
        var currency = Enc(invoice.Currency);

        var rows = new StringBuilder();
        void Row(string label, string detail, decimal amount) =>
            rows.Append($"<tr><td>{Enc(label)}</td><td class=\"detail\">{Enc(detail)}</td><td class=\"num\">{Money(amount)}</td></tr>");

        Row(invoice.PlanNameAr, $"{Date(invoice.PeriodStart)} → {Date(invoice.PeriodEnd)}", invoice.BaseAmount);
        if (invoice.ExtraBranches > 0)
            Row(L("فروع إضافية", "Extra branches"), $"× {invoice.ExtraBranches}", invoice.ExtraBranchesAmount);
        if (invoice.ExtraUsers > 0)
            Row(L("مستخدمون / موظفون إضافيون", "Extra users / employees"), $"× {invoice.ExtraUsers}", invoice.ExtraUsersAmount);
        if (invoice.MultiCompanyDiscountAmount != 0)
            Row(L("خصم تعدد الشركات", "Multi-company discount"), string.Empty, -Math.Abs(invoice.MultiCompanyDiscountAmount));

        var paid = invoice.Status == SubscriptionInvoiceStatus.Paid;
        var statusLabel = invoice.Status switch
        {
            SubscriptionInvoiceStatus.Paid => L("مدفوعة", "Paid"),
            SubscriptionInvoiceStatus.Cancelled => L("ملغاة", "Cancelled"),
            SubscriptionInvoiceStatus.Failed => L("فشل السداد", "Payment failed"),
            _ => L("في انتظار السداد", "Awaiting payment"),
        };
        var statusClass = paid ? "paid" : invoice.Status == SubscriptionInvoiceStatus.Pending ? "pending" : "other";

        var paymentLine = paid
            ? $"{L("تاريخ السداد", "Paid on")}: <b><bdi>{(invoice.PaidAtUtc.HasValue ? Date(invoice.PaidAtUtc.Value) : "—")}</bdi></b>" +
              (string.IsNullOrWhiteSpace(invoice.PaymentReference) ? "" : $"<br />{L("المرجع", "Reference")}: <b>{Enc(invoice.PaymentReference)}</b>")
            : $"{L("تاريخ الاستحقاق", "Due date")}: <b><bdi>{Date(invoice.DueDateUtc)}</bdi></b>";

        var logo = string.IsNullOrEmpty(logoPngBase64)
            ? "<div class=\"logo-fallback\">R</div>"
            : $"<img class=\"logo\" src=\"data:image/png;base64,{logoPngBase64}\" alt=\"Roma Group\" />";

        var dir = arabic ? "rtl" : "ltr";
        var align = arabic ? "right" : "left";
        var oppositeAlign = arabic ? "left" : "right";

        return $$"""
            <!doctype html>
            <html dir="{{dir}}" lang="{{(arabic ? "ar" : "en")}}">
            <head>
            <meta charset="utf-8" />
            <style>
                @page { size: A4; margin: 0; }
                * { box-sizing: border-box; }
                body { font-family: "Segoe UI", Tahoma, Arial, sans-serif; color: #1c2620; margin: 0; font-size: 13px; }
                .page { padding: 28px 34px; }
                .top { display: flex; justify-content: space-between; align-items: center; border-bottom: 3px solid #0f5c4b; padding-bottom: 16px; margin-bottom: 22px; }
                .brand { display: flex; align-items: center; gap: 12px; }
                .logo { width: 56px; height: 56px; border-radius: 14px; }
                .logo-fallback { width: 56px; height: 56px; border-radius: 14px; background: #0f5c4b; color: #fff; font: 700 30px Georgia, serif; display: flex; align-items: center; justify-content: center; }
                .brand-name { font-size: 22px; font-weight: 700; color: #0f5c4b; margin: 0; }
                .brand-sub { font-size: 12px; color: #5b6a60; margin: 2px 0 0; }
                .title { text-align: {{oppositeAlign}}; }
                .title h1 { margin: 0 0 6px; font-size: 22px; color: #0f5c4b; }
                .meta { font-size: 12px; color: #444; line-height: 1.9; }
                .parties { display: flex; gap: 16px; margin-bottom: 20px; }
                .box { flex: 1; background: #f4f1ea; border-radius: 8px; padding: 12px 16px; }
                .box .label { font-size: 11px; color: #777; margin-bottom: 3px; }
                .box .name { font-size: 15px; font-weight: 700; }
                .status { display: inline-block; padding: 3px 12px; border-radius: 999px; font-weight: 700; font-size: 12px; }
                .status.paid { background: #e1f0e8; color: #0f5c4b; }
                .status.pending { background: #fbefd9; color: #8a5a12; }
                .status.other { background: #f6e1dc; color: #a23b27; }
                table { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
                thead th { background: #0f5c4b; color: #fff; padding: 8px 10px; font-size: 12px; text-align: {{align}}; }
                thead th.num, td.num { text-align: {{oppositeAlign}}; font-variant-numeric: tabular-nums; }
                tbody td { padding: 9px 10px; border-bottom: 1px solid #e2ddd0; }
                td.detail { color: #5b6a60; font-size: 12px; }
                .total { width: 300px; margin-inline-start: auto; border-top: 2px solid #0f5c4b; padding-top: 10px; display: flex; justify-content: space-between; font-size: 17px; font-weight: 700; }
                .foot { margin-top: 40px; text-align: center; font-size: 11px; color: #8a8f8b; line-height: 1.8; }
            </style>
            </head>
            <body>
              <div class="page">
                <div class="top">
                  <div class="brand">
                    {{logo}}
                    <div>
                      <p class="brand-name">Roma Group</p>
                      <p class="brand-sub">romagroup.app · support@romagroup.app</p>
                    </div>
                  </div>
                  <div class="title">
                    <h1>{{L("فاتورة اشتراك", "Subscription invoice")}}</h1>
                    <div class="meta">
                      {{L("رقم الفاتورة", "Invoice no.")}}: <b>{{Enc(NumberFor(invoice))}}</b><br />
                      {{paymentLine}}
                    </div>
                  </div>
                </div>

                <div class="parties">
                  <div class="box">
                    <div class="label">{{L("العميل", "Billed to")}}</div>
                    <div class="name">{{Enc(invoice.CompanyNameAr)}}</div>
                  </div>
                  <div class="box">
                    <div class="label">{{L("الحالة", "Status")}}</div>
                    <span class="status {{statusClass}}">{{Enc(statusLabel)}}</span>
                  </div>
                </div>

                <table>
                  <thead><tr><th>{{L("البند", "Item")}}</th><th>{{L("التفاصيل", "Details")}}</th><th class="num">{{L("المبلغ", "Amount")}} ({{currency}})</th></tr></thead>
                  <tbody>{{rows}}</tbody>
                </table>

                <div class="total"><span>{{L("الإجمالي", "Total")}}</span><bdi dir="ltr">{{Money(invoice.TotalAmount)}} {{currency}}</bdi></div>

                <div class="foot">
                  {{L("شكرًا لاستخدامك منتجات Roma Group.", "Thank you for using Roma Group products.")}}<br />
                  {{L("لأي استفسار عن الفاتورة: support@romagroup.app", "Questions about this invoice: support@romagroup.app")}}
                </div>
              </div>
            </body>
            </html>
            """;
    }
}
