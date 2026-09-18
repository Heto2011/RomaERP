using RomaERP.Application.Purchasing.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Purchasing;
using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class PurchaseInvoicePdfTests
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

    private static PurchaseInvoice Invoice(Vendor vendor, Account account, string? notes = null) => new()
    {
        InvoiceNumber = "PI-000001",
        InvoiceDate = new DateTime(2026, 8, 24),
        Vendor = vendor,
        VendorId = vendor.Id,
        SubTotal = 300,
        VatRate = 0.14m,
        VatAmount = 42,
        TotalAmount = 342,
        PaymentTerm = PaymentTerm.Cash,
        PaidAmount = 342,
        Notes = notes,
        Lines = new List<PurchaseInvoiceLine>
        {
            new() { LineNumber = 1, Description = "إيجار", Account = account, AccountId = account.Id, Quantity = 1, UnitPrice = 300, LineTotal = 300 }
        }
    };

    [Fact]
    public void Build_EscapesHtmlInjectionAttemptsInUserSuppliedFields()
    {
        var vendor = new Vendor { Code = "V1", NameAr = "<script>alert(1)</script>", NameEn = "Evil" };
        var account = new Account { Code = "5300", NameAr = "مصروفات إدارية", NameEn = "Admin", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var invoice = Invoice(vendor, account, notes: "\"><img src=x onerror=alert(1)>");
        invoice.Lines.First().Description = "<b>bold</b> & \"quoted\"";

        var html = PurchaseInvoiceHtmlTemplate.Build(invoice, Settings());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
        Assert.DoesNotContain("<b>bold</b>", html);
    }

    [Fact]
    public void Build_IncludesInvoiceNumberVendorAndTotals()
    {
        var vendor = new Vendor { Code = "V1", NameAr = "مورد تجريبي", NameEn = "Test Vendor", TaxRegistrationNumber = "999" };
        var account = new Account { Code = "5300", NameAr = "مصروفات إدارية", NameEn = "Admin", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var invoice = Invoice(vendor, account);

        var html = PurchaseInvoiceHtmlTemplate.Build(invoice, Settings());

        Assert.Contains("PI-000001", html);
        Assert.Contains("مورد تجريبي", html);
        Assert.Contains("999", html);
        Assert.Contains("342.00", html);
        Assert.Contains("300.00", html);
        Assert.Contains("42.00", html);
    }
}
