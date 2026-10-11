using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Purchasing;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class InvoiceScanServiceTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Vendor V(string code, string nameAr, string nameEn, string? tax = null)
        => new() { Code = code, NameAr = nameAr, NameEn = nameEn, TaxRegistrationNumber = tax };

    [Fact]
    public void VendorMatcher_PrefersTaxNumber_ThenNormalizedName()
    {
        var vendors = new List<Vendor>
        {
            V("V1", "شركة الأمل للتوريدات", "Al Amal Supplies", "123-456-789"),
            V("V2", "مخبز النور", "Al Nour Bakery", "555-111-222"),
        };

        // Tax number wins even when the scanned name is unrelated, and punctuation is ignored.
        Assert.Equal("V2", VendorMatcher.Match(vendors, "اسم غريب", "555 111 222")?.Code);
        // Arabic letter variants fold (أ→ا, ة→ه) so the spelling on the paper still matches.
        Assert.Equal("V1", VendorMatcher.Match(vendors, "شركه الامل للتوريدات", null)?.Code);
        // Scanned text containing the vendor's name matches too.
        Assert.Equal("V2", VendorMatcher.Match(vendors, "مخبز النور - فرع المعادي", null)?.Code);
        Assert.Null(VendorMatcher.Match(vendors, "مورد جديد خالص", null));
        Assert.Null(VendorMatcher.Match(vendors, null, null));
    }

    [Fact]
    public async Task Scan_MatchesVendorAndAccount_AndBuildsSingleLineFromNetWhenLinesMissing()
    {
        var ctx = CreateContext();
        var admin = new Account { Code = "5300", NameAr = "مصروفات إدارية وعمومية", NameEn = "Admin", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var vendor = V("V1", "شركة الأمل للتوريدات", "Al Amal Supplies", "123456789");
        ctx.Accounts.Add(admin);
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        var parser = new FakeClaudeExpenseParser
        {
            InvoiceHandler = () => new InvoiceScanExtraction(
                "الأمل للتوريدات", "123456789", "INV-77", new DateTime(2026, 9, 3), "EGP",
                SubTotal: null, VatAmount: 140m, VatRatePercent: 14m, Total: 1140m,
                SuggestedAccountCode: "5300", Lines: new List<InvoiceScanLine>(), NeedsClarification: false, ClarifyingQuestion: null)
        };

        var result = await new InvoiceScanService(ctx, parser).ScanAsync(new byte[] { 1 }, "image/png");

        Assert.Equal(vendor.Id, result.MatchedVendorId);
        Assert.Equal(admin.Id, result.SuggestedAccountId);
        Assert.Equal(1000m, result.SubTotal); // total - VAT
        Assert.Equal("INV-77", result.InvoiceNumber);
        var line = Assert.Single(result.Lines);
        Assert.Equal(1000m, line.UnitPrice);
        Assert.False(result.NeedsClarification);
    }

    [Fact]
    public async Task Scan_KeepsParsedLines_AndLeavesUnknownVendorUnmatched()
    {
        var ctx = CreateContext();
        var parser = new FakeClaudeExpenseParser
        {
            InvoiceHandler = () => new InvoiceScanExtraction(
                "مورد مجهول", null, null, null, null, 300m, 42m, 14m, 342m, "9999",
                new List<InvoiceScanLine> { new("دقيق", 10, 20), new("سكر", 5, 20) }, false, null)
        };

        var result = await new InvoiceScanService(ctx, parser).ScanAsync(new byte[] { 1 }, "image/jpeg");

        Assert.Null(result.MatchedVendorId);
        Assert.Null(result.SuggestedAccountId); // code not in the chart of accounts -> not guessed
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(10m, result.Lines[0].Quantity);
    }
}
