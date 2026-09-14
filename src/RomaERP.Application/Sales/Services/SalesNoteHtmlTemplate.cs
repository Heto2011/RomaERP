using System.Globalization;
using System.Net;
using System.Text;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Sales.Services;

/// <summary>Builds the printable HTML for a credit/debit note, rendered to PDF via IHtmlToPdfRenderer. Same
/// HTML-encoding discipline as SalesInvoiceHtmlTemplate — every user-supplied string is escaped before
/// insertion into a document a real headless browser renders server-side.</summary>
public static class SalesNoteHtmlTemplate
{
    public static string Build(SalesNote note, CompanySettings settings)
    {
        var customer = note.Customer!;
        var originalInvoice = note.OriginalInvoice!;
        var culture = CultureInfo.InvariantCulture;

        string Money(decimal amount) => amount.ToString("N2", culture);
        string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var isCredit = note.NoteType == SalesNoteType.Credit;
        var titleAr = isCredit ? "إشعار دائن" : "إشعار مدين";
        var accentColor = isCredit ? "#8a1f11" : "#132C39";
        var currency = Enc(settings.DefaultCurrency);

        var linesHtml = new StringBuilder();
        foreach (var line in note.Lines.OrderBy(l => l.LineNumber))
        {
            linesHtml.Append("<tr>");
            linesHtml.Append($"<td class=\"num\">{line.LineNumber}</td>");
            linesHtml.Append($"<td>{Enc(line.Description)}</td>");
            linesHtml.Append($"<td class=\"num\">{line.Quantity.ToString("0.####", culture)}</td>");
            linesHtml.Append($"<td class=\"num\">{Money(line.UnitPrice)}</td>");
            linesHtml.Append($"<td class=\"num\">{Money(line.LineTotal)}</td>");
            linesHtml.Append("</tr>");
        }

        var companyTaxLine = string.IsNullOrWhiteSpace(settings.TaxRegistrationNumber)
            ? string.Empty
            : $"<p class=\"company-tax\">الرقم الضريبي: {Enc(settings.TaxRegistrationNumber)}</p>";

        var customerTaxLine = string.IsNullOrWhiteSpace(customer.TaxRegistrationNumber)
            ? string.Empty
            : $"<div class=\"label\">الرقم الضريبي: {Enc(customer.TaxRegistrationNumber)}</div>";

        return $$"""
            <!doctype html>
            <html dir="rtl" lang="ar">
            <head>
            <meta charset="utf-8" />
            <style>
                @page { size: A4; margin: 0; }
                * { box-sizing: border-box; }
                body { font-family: "Segoe UI", Tahoma, Arial, sans-serif; color: #1a1a1a; margin: 0; font-size: 13px; }
                .header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid {{accentColor}}; padding-bottom: 14px; margin-bottom: 18px; }
                .company-name { font-size: 22px; font-weight: 700; color: {{accentColor}}; margin: 0 0 4px; }
                .company-name-en { font-size: 13px; color: #666; margin: 0; }
                .company-tax { font-size: 12px; color: #444; margin-top: 6px; }
                .note-title { text-align: left; }
                .note-title h1 { font-size: 20px; margin: 0; color: {{accentColor}}; }
                .note-meta { font-size: 12px; color: #444; margin-top: 6px; line-height: 1.8; }
                .note-meta b { color: #1a1a1a; }
                .customer-box { background: #f5f6f7; border-radius: 6px; padding: 12px 16px; margin-bottom: 18px; }
                .customer-box .label { font-size: 11px; color: #777; margin-bottom: 2px; }
                .customer-box .name { font-size: 15px; font-weight: 600; }
                .reason-box { background: #fbf3f1; border-radius: 6px; padding: 10px 16px; margin-bottom: 18px; font-size: 12.5px; }
                table { width: 100%; border-collapse: collapse; margin-bottom: 18px; }
                thead th { background: {{accentColor}}; color: #fff; padding: 8px 10px; font-size: 12px; text-align: right; }
                thead th.num, td.num { text-align: center; }
                tbody td { padding: 7px 10px; border-bottom: 1px solid #e5e5e5; font-size: 12.5px; }
                .totals { width: 280px; margin-inline-start: auto; }
                .totals table { margin-bottom: 0; }
                .totals td { padding: 6px 10px; font-size: 13px; border: none; }
                .totals tr.grand-total td { font-weight: 700; font-size: 15px; border-top: 2px solid {{accentColor}}; padding-top: 10px; }
                .footer { margin-top: 30px; text-align: center; font-size: 10.5px; color: #999; }
                .page { padding: 24px 28px; }
            </style>
            </head>
            <body>
                <div class="page">
                    <div class="header">
                        <div>
                            <p class="company-name">{{Enc(settings.CompanyNameAr)}}</p>
                            <p class="company-name-en">{{Enc(settings.CompanyNameEn)}}</p>
                            {{companyTaxLine}}
                        </div>
                        <div class="note-title">
                            <h1>{{titleAr}}</h1>
                            <div class="note-meta">
                                رقم الإشعار: <b>{{Enc(note.NoteNumber)}}</b><br />
                                التاريخ: <b>{{note.NoteDate:yyyy-MM-dd}}</b><br />
                                مرجع الفاتورة: <b>{{Enc(originalInvoice.InvoiceNumber)}}</b>
                            </div>
                        </div>
                    </div>

                    <div class="customer-box">
                        <div class="label">العميل</div>
                        <div class="name">{{Enc(customer.NameAr)}}</div>
                        {{customerTaxLine}}
                    </div>

                    <div class="reason-box"><strong>السبب:</strong> {{Enc(note.Reason)}}</div>

                    <table>
                        <thead>
                            <tr>
                                <th class="num">#</th>
                                <th>البيان</th>
                                <th class="num">الكمية</th>
                                <th class="num">سعر الوحدة</th>
                                <th class="num">الإجمالي</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{linesHtml}}
                        </tbody>
                    </table>

                    <div class="totals">
                        <table>
                            <tr><td>الصافي</td><td class="num">{{Money(note.SubTotal)}} {{currency}}</td></tr>
                            <tr><td>الضريبة ({{(note.VatRate * 100).ToString("0.##", culture)}}%)</td><td class="num">{{Money(note.VatAmount)}} {{currency}}</td></tr>
                            <tr class="grand-total"><td>الإجمالي</td><td class="num">{{Money(note.TotalAmount)}} {{currency}}</td></tr>
                        </table>
                    </div>

                    <div class="footer">تم إصدار هذا الإشعار عن طريق نظام RomaERP</div>
                </div>
            </body>
            </html>
            """;
    }
}
