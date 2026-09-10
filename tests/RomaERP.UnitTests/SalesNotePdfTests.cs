using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class SalesNotePdfTests
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

    private static SalesInvoice OriginalInvoice(Customer customer) => new()
    {
        InvoiceNumber = "SI-000001",
        InvoiceDate = new DateTime(2026, 8, 20),
        Customer = customer,
        CustomerId = customer.Id,
        SubTotal = 200,
        VatRate = 0.14m,
        VatAmount = 28,
        TotalAmount = 228,
        PaymentTerm = PaymentTerm.Cash,
        PaidAmount = 228
    };

    private static SalesNote Note(Customer customer, SalesInvoice originalInvoice, SalesNoteType type = SalesNoteType.Credit, string? notes = null) => new()
    {
        NoteNumber = type == SalesNoteType.Credit ? "CN-000001" : "DN-000001",
        NoteType = type,
        NoteDate = new DateTime(2026, 8, 24),
        Customer = customer,
        CustomerId = customer.Id,
        OriginalInvoice = originalInvoice,
        OriginalInvoiceId = originalInvoice.Id,
        Reason = "إرجاع بضاعة تالفة",
        SubTotal = 50,
        VatRate = 0.14m,
        VatAmount = 7,
        TotalAmount = 57,
        Notes = notes,
        Lines = new List<SalesNoteLine>
        {
            new() { LineNumber = 1, Description = "منتج مرتجع", Quantity = 1, UnitPrice = 50, LineTotal = 50 }
        }
    };

    [Fact]
    public void Build_EscapesHtmlInjectionAttemptsInUserSuppliedFields()
    {
        var customer = new Customer { Code = "C1", NameAr = "<script>alert(1)</script>", NameEn = "Evil" };
        var note = Note(customer, OriginalInvoice(customer), notes: "\"><img src=x onerror=alert(1)>");
        note.Reason = "<b>سبب خبيث</b>";
        note.Lines.First().Description = "<b>bold</b> & \"quoted\"";

        var html = SalesNoteHtmlTemplate.Build(note, Settings());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
        Assert.DoesNotContain("<b>bold</b>", html);
        Assert.DoesNotContain("<b>سبب خبيث</b>", html);
    }

    [Fact]
    public void Build_IncludesNoteNumberOriginalInvoiceAndTotals()
    {
        var customer = new Customer { Code = "C1", NameAr = "عميل تجريبي", NameEn = "Test Customer", TaxRegistrationNumber = "999" };
        var original = OriginalInvoice(customer);
        var note = Note(customer, original);

        var html = SalesNoteHtmlTemplate.Build(note, Settings());

        Assert.Contains("CN-000001", html);
        Assert.Contains("SI-000001", html);
        Assert.Contains("عميل تجريبي", html);
        Assert.Contains("إرجاع بضاعة تالفة", html);
        Assert.Contains("57.00", html);
        Assert.Contains("50.00", html);
        Assert.Contains("7.00", html);
    }

    [Fact]
    public void Build_DebitNote_UsesDebitTitle()
    {
        var customer = new Customer { Code = "C1", NameAr = "عميل", NameEn = "Customer" };
        var note = Note(customer, OriginalInvoice(customer), SalesNoteType.Debit);

        var html = SalesNoteHtmlTemplate.Build(note, Settings());

        Assert.Contains("إشعار مدين", html);
        Assert.Contains("DN-000001", html);
    }
}
