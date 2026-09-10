using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Pdf;
using Xunit;

namespace RomaERP.UnitTests;

public class SalesInvoicePdfTests
{
    private static CompanySettings Settings() => new()
    {
        CompanyNameAr = "شركة روما التجريبية",
        CompanyNameEn = "Roma Test Co",
        Country = Country.Egypt,
        TaxRegistrationNumber = "123456789",
        VatRate = 0.14m,
        DefaultCurrency = "EGP",
    };

    private static SalesInvoice Invoice(Customer customer, string? notes = null) => new()
    {
        InvoiceNumber = "SI-000001",
        InvoiceDate = new DateTime(2026, 8, 24),
        Customer = customer,
        CustomerId = customer.Id,
        SubTotal = 200,
        VatRate = 0.14m,
        VatAmount = 28,
        TotalAmount = 228,
        PaymentTerm = PaymentTerm.Cash,
        PaidAmount = 228,
        Notes = notes,
        Lines = new List<SalesInvoiceLine>
        {
            new() { LineNumber = 1, Description = "منتج تجريبي", Quantity = 2, UnitPrice = 100, LineTotal = 200 }
        }
    };

    [Fact]
    public void Build_EscapesHtmlInjectionAttemptsInUserSuppliedFields()
    {
        var customer = new Customer { Code = "C1", NameAr = "<script>alert(1)</script>", NameEn = "Evil" };
        var invoice = Invoice(customer, notes: "\"><img src=x onerror=alert(1)>");
        invoice.Lines.First().Description = "<b>bold</b> & \"quoted\"";

        var html = SalesInvoiceHtmlTemplate.Build(invoice, Settings());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
        Assert.DoesNotContain("<b>bold</b>", html);
    }

    [Fact]
    public void Build_IncludesInvoiceNumberCustomerAndTotals()
    {
        var customer = new Customer { Code = "C1", NameAr = "عميل تجريبي", NameEn = "Test Customer", TaxRegistrationNumber = "999" };
        var invoice = Invoice(customer);

        var html = SalesInvoiceHtmlTemplate.Build(invoice, Settings());

        Assert.Contains("SI-000001", html);
        Assert.Contains("عميل تجريبي", html);
        Assert.Contains("999", html);
        Assert.Contains("228.00", html);
        Assert.Contains("200.00", html);
        Assert.Contains("28.00", html);
    }

    [Fact]
    public void Build_ShowsOutstandingOnlyForCreditInvoices()
    {
        var customer = new Customer { Code = "C1", NameAr = "عميل", NameEn = "Customer" };
        var cashInvoice = Invoice(customer);
        var creditInvoice = Invoice(customer);
        creditInvoice.PaymentTerm = PaymentTerm.Credit;
        creditInvoice.PaidAmount = 100;

        var cashHtml = SalesInvoiceHtmlTemplate.Build(cashInvoice, Settings());
        var creditHtml = SalesInvoiceHtmlTemplate.Build(creditInvoice, Settings());

        Assert.DoesNotContain("المتبقي", cashHtml);
        Assert.Contains("المتبقي", creditHtml);
        Assert.Contains("128.00", creditHtml); // 228 - 100 outstanding
    }

    [Fact(Skip = "Requires a Chromium binary matching this Microsoft.Playwright package's expected revision — " +
        "manually verified passing in the dev sandbox via ROMAERP_TEST_CHROMIUM_PATH; run explicitly to re-verify.")]
    public async Task PlaywrightHtmlToPdfRenderer_RendersValidPdfBytes()
    {
        var customer = new Customer { Code = "C1", NameAr = "عميل تجريبي", NameEn = "Test Customer" };
        var html = SalesInvoiceHtmlTemplate.Build(Invoice(customer), Settings());

        // This sandbox's pre-installed Chromium revision doesn't match the Microsoft.Playwright NuGet
        // version's expected revision, so point at it explicitly here — see the constructor's own doc comment.
        var chromiumPath = Environment.GetEnvironmentVariable("ROMAERP_TEST_CHROMIUM_PATH") ?? "/opt/pw-browsers/chromium-1194/chrome-linux/chrome";
        await using var renderer = new PlaywrightHtmlToPdfRenderer(chromiumPath);
        var pdfBytes = await renderer.RenderAsync(html);

        Assert.True(pdfBytes.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), pdfBytes[..4]);
    }
}
