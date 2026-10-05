using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Roma Group's own subscription invoice: correct figures, safe HTML, brand logo, both languages.</summary>
public class SubscriptionInvoiceHtmlTemplateTests
{
    private static SubscriptionInvoiceDto Invoice(
        string company = "شركة تجريبية", SubscriptionInvoiceStatus status = SubscriptionInvoiceStatus.Pending,
        decimal discount = 0, int extraUsers = 0, decimal extraUsersAmount = 0) =>
        new(Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001"), Guid.NewGuid(), company, "people", "باقة Roma HR",
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), 99m, 0, 0m, extraUsers, extraUsersAmount, discount,
            99m + extraUsersAmount - discount, "SAR", status, new DateTime(2026, 10, 8),
            status == SubscriptionInvoiceStatus.Paid ? new DateTime(2026, 10, 5) : null,
            status == SubscriptionInvoiceStatus.Paid ? "REF-77" : null);

    [Fact]
    public void Number_IsStableAndReadable()
        => Assert.Equal("RG-202610-A1B2C3", SubscriptionInvoiceHtmlTemplate.NumberFor(Invoice()));

    [Fact]
    public void Build_ShowsTotalCurrencyAndBrand()
    {
        var html = SubscriptionInvoiceHtmlTemplate.Build(Invoice(), "AAAA", arabic: false);
        Assert.Contains("Subscription invoice", html);
        Assert.Contains("99.00 SAR", html);
        Assert.Contains("RG-202610-A1B2C3", html);
        Assert.Contains("data:image/png;base64,AAAA", html);
        Assert.Contains("Roma Group", html);
        Assert.Contains("Awaiting payment", html);
        Assert.Contains("2026-10-08", html);
    }

    [Fact]
    public void Build_PaidInvoiceShowsPaymentDateAndReference()
    {
        var html = SubscriptionInvoiceHtmlTemplate.Build(Invoice(status: SubscriptionInvoiceStatus.Paid), null, arabic: false);
        Assert.Contains(">Paid<", html);
        Assert.Contains("2026-10-05", html);
        Assert.Contains("REF-77", html);
        Assert.Contains("logo-fallback", html); // no logo bytes supplied
    }

    [Fact]
    public void Build_Arabic_IsRtlAndLabelled()
    {
        var html = SubscriptionInvoiceHtmlTemplate.Build(Invoice(), null, arabic: true);
        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains("فاتورة اشتراك", html);
    }

    [Fact]
    public void Build_ListsExtrasAndShowsDiscountAsNegative()
    {
        var html = SubscriptionInvoiceHtmlTemplate.Build(Invoice(discount: 10m, extraUsers: 3, extraUsersAmount: 15m), null, arabic: false);
        Assert.Contains("Extra users / employees", html);
        Assert.Contains("× 3", System.Net.WebUtility.HtmlDecode(html)); // the encoder writes × as &#215;
        Assert.Contains("-10.00", html);
        Assert.Contains("104.00 SAR", html);
    }

    [Fact]
    public void Build_EncodesCompanyName()
    {
        var html = SubscriptionInvoiceHtmlTemplate.Build(Invoice(company: "<script>alert(1)</script>"), null, arabic: false);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
