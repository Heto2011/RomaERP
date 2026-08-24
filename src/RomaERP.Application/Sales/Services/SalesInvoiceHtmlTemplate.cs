using System.Globalization;
using System.Net;
using System.Text;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Sales.Services;

/// <summary>Builds the printable HTML for a sales invoice, rendered to PDF via IHtmlToPdfRenderer. Every
/// user-supplied string (customer name, line descriptions, notes, company name) is HTML-encoded before
/// insertion — this HTML is rendered by a real headless browser server-side, so unescaped user text would be
/// a genuine HTML/script injection risk, not just a cosmetic one.</summary>
public static class SalesInvoiceHtmlTemplate
{
    public static string Build(SalesInvoice invoice, CompanySettings settings)
    {
        var customer = invoice.Customer!;
        var culture = CultureInfo.InvariantCulture;

        string Money(decimal amount) => amount.ToString("N2", culture);
        string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var paymentTermLabel = invoice.PaymentTerm switch
        {
            PaymentTerm.Cash => "كاش",
            PaymentTerm.Card => "شبكة",
            PaymentTerm.Credit => "آجل",
            _ => invoice.PaymentTerm.ToString()
        };
        var currency = Enc(settings.DefaultCurrency);

        var linesHtml = new StringBuilder();
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
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

        var creditRows = invoice.PaymentTerm != PaymentTerm.Credit
            ? string.Empty
            : $"<tr><td>المدفوع</td><td class=\"num\">{Money(invoice.PaidAmount)} {currency}</td></tr>" +
              $"<tr><td>المتبقي</td><td class=\"num\">{Money(invoice.TotalAmount - invoice.PaidAmount)} {currency}</td></tr>";

        var notesHtml = string.IsNullOrWhiteSpace(invoice.Notes)
            ? string.Empty
            : $"<div class=\"notes\"><strong>ملاحظات:</strong> {Enc(invoice.Notes)}</div>";

        return $$"""
            <!doctype html>
            <html dir="rtl" lang="ar">
            <head>
            <meta charset="utf-8" />
            <style>
                @page { size: A4; margin: 0; }
                * { box-sizing: border-box; }
                body { font-family: "Segoe UI", Tahoma, Arial, sans-serif; color: #1a1a1a; margin: 0; font-size: 13px; }
                .header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid #132C39; padding-bottom: 14px; margin-bottom: 18px; }
                .company-name { font-size: 22px; font-weight: 700; color: #132C39; margin: 0 0 4px; }
                .company-name-en { font-size: 13px; color: #666; margin: 0; }
                .company-tax { font-size: 12px; color: #444; margin-top: 6px; }
                .invoice-title { text-align: left; }
                .invoice-title h1 { font-size: 20px; margin: 0; color: #132C39; }
                .invoice-meta { font-size: 12px; color: #444; margin-top: 6px; line-height: 1.8; }
                .invoice-meta b { color: #1a1a1a; }
                .customer-box { background: #f5f6f7; border-radius: 6px; padding: 12px 16px; margin-bottom: 18px; }
                .customer-box .label { font-size: 11px; color: #777; margin-bottom: 2px; }
                .customer-box .name { font-size: 15px; font-weight: 600; }
                table { width: 100%; border-collapse: collapse; margin-bottom: 18px; }
                thead th { background: #132C39; color: #fff; padding: 8px 10px; font-size: 12px; text-align: right; }
                thead th.num, td.num { text-align: center; }
                tbody td { padding: 7px 10px; border-bottom: 1px solid #e5e5e5; font-size: 12.5px; }
                .totals { width: 280px; margin-inline-start: auto; }
                .totals table { margin-bottom: 0; }
                .totals td { padding: 6px 10px; font-size: 13px; border: none; }
                .totals tr.grand-total td { font-weight: 700; font-size: 15px; border-top: 2px solid #132C39; padding-top: 10px; }
                .notes { margin-top: 18px; font-size: 12px; color: #444; background: #fbf8f3; padding: 10px 14px; border-radius: 6px; }
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
                        <div class="invoice-title">
                            <h1>فاتورة مبيعات</h1>
                            <div class="invoice-meta">
                                رقم الفاتورة: <b>{{Enc(invoice.InvoiceNumber)}}</b><br />
                                التاريخ: <b>{{invoice.InvoiceDate:yyyy-MM-dd}}</b><br />
                                طريقة السداد: <b>{{Enc(paymentTermLabel)}}</b>
                            </div>
                        </div>
                    </div>

                    <div class="customer-box">
                        <div class="label">العميل</div>
                        <div class="name">{{Enc(customer.NameAr)}}</div>
                        {{customerTaxLine}}
                    </div>

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
                            <tr><td>الصافي</td><td class="num">{{Money(invoice.SubTotal)}} {{currency}}</td></tr>
                            <tr><td>الضريبة ({{(invoice.VatRate * 100).ToString("0.##", culture)}}%)</td><td class="num">{{Money(invoice.VatAmount)}} {{currency}}</td></tr>
                            <tr class="grand-total"><td>الإجمالي</td><td class="num">{{Money(invoice.TotalAmount)}} {{currency}}</td></tr>
                            {{creditRows}}
                        </table>
                    </div>

                    {{notesHtml}}

                    <div class="footer">تم إصدار هذه الفاتورة عن طريق نظام RomaERP</div>
                </div>
            </body>
            </html>
            """;
    }
}
