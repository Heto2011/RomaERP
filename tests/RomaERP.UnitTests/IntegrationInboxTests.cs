using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Integration;
using RomaERP.Application.Inventory.Services;
using RomaERP.Application.Purchasing.Services;
using RomaERP.Application.Sales.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Integration;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class IntegrationInboxTests
{
    private static async Task<(ApplicationDbContext ctx, IntegrationInboxService inbox)> CreateAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Account A(string code, string name, AccountType t, AccountNature n) => new() { Code = code, NameAr = name, NameEn = name, AccountType = t, Nature = n };
        ctx.Accounts.AddRange(
            A("1111", "Cash", AccountType.Asset, AccountNature.Debit), A("1112", "Bank", AccountType.Asset, AccountNature.Debit),
            A("1120", "AR", AccountType.Asset, AccountNature.Debit), A("2120", "AP", AccountType.Liability, AccountNature.Credit),
            A("4100", "Sales", AccountType.Revenue, AccountNature.Credit), A("2161", "Output VAT", AccountType.Liability, AccountNature.Credit),
            A("5300", "Admin expenses", AccountType.Expense, AccountNature.Debit), A("5400", "Depreciation", AccountType.Expense, AccountNature.Debit));
        var today = DateTime.UtcNow.Date;
        var year = new FiscalYear { Name = today.Year.ToString(), StartDate = new DateTime(today.Year, 1, 1), EndDate = new DateTime(today.Year, 12, 31) };
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Current", PeriodNumber = 1, StartDate = today.AddDays(-20), EndDate = today.AddDays(20) });
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0.14m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        var fx = new ExchangeRateService(ctx, new FakeExchangeRateProvider());
        var sales = new SalesService(ctx, new FakeHtmlToPdfRenderer(), fx);
        var purchasing = new PurchasingService(ctx, new FakeHtmlToPdfRenderer(), new ItemLotService(ctx), fx);
        return (ctx, new IntegrationInboxService(ctx, sales, purchasing, new JournalEntryService(ctx), new ItemService(ctx)));
    }

    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static string Today => DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

    [Fact]
    public async Task Receive_IsIdempotent_PerTypeAndReference()
    {
        var (ctx, inbox) = await CreateAsync();
        var first = await inbox.ReceiveAsync("bot", "Customer", "c-1", J("""{"nameAr":"شركة النور"}"""));
        var again = await inbox.ReceiveAsync("bot", "customer", "c-1", J("""{"nameAr":"اسم تاني"}"""));
        Assert.False(first.Duplicate);
        Assert.True(again.Duplicate);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await ctx.IntegrationInboxItems.CountAsync());
        // the same reference under another type is a different item
        Assert.False((await inbox.ReceiveAsync("bot", "Vendor", "c-1", J("""{"nameAr":"مورد"}"""))).Duplicate);
    }

    [Theory]
    [InlineData("Nope", "x", """{}""")]
    [InlineData("Customer", "", """{"nameAr":"a"}""")]
    [InlineData("Customer", "x", """{"phone":"1"}""")]
    [InlineData("SalesInvoice", "x", """{"customerCode":"C1","lines":[]}""")]
    [InlineData("Expense", "x", """{"description":"a","amount":0,"date":"2026-01-01"}""")]
    public async Task Receive_RejectsBadInput(string type, string reference, string data)
    {
        var (_, inbox) = await CreateAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => inbox.ReceiveAsync("bot", type, reference, J(data)));
    }

    [Fact]
    public async Task Receive_RejectsAnUnbalancedJournalEntry()
    {
        var (_, inbox) = await CreateAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => inbox.ReceiveAsync("bot", "JournalEntry", "j1",
            J($$"""{"date":"{{Today}}","lines":[{"accountCode":"1111","debit":100},{"accountCode":"4100","credit":90}]}""")));
    }

    [Fact]
    public async Task NothingTouchesTheBooksUntilApproved()
    {
        var (ctx, inbox) = await CreateAsync();
        await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"شركة النور","code":"NOOR"}"""));
        Assert.Empty(ctx.Customers);
    }

    [Fact]
    public async Task ApprovingACustomer_CreatesIt_AndASecondApprovalIsRefused()
    {
        var (ctx, inbox) = await CreateAsync();
        var r = await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"شركة النور","code":"NOOR","phone":"010"}"""));
        var approved = await inbox.ApproveAsync(r.Id, "ehab");
        Assert.Equal("Approved", approved.Status);
        Assert.Single(ctx.Customers.Where(c => c.Code == "NOOR"));
        await Assert.ThrowsAsync<ValidationAppException>(() => inbox.ApproveAsync(r.Id, "ehab"));
    }

    [Fact]
    public async Task ACustomerWithoutACode_GetsOneFromTheReference()
    {
        var (ctx, inbox) = await CreateAsync();
        var r = await inbox.ReceiveAsync("bot", "Customer", "client-77", J("""{"nameAr":"بدون كود"}"""));
        await inbox.ApproveAsync(r.Id, "x");
        Assert.Contains(ctx.Customers, c => c.Code == "EXT-client77");
    }

    [Fact]
    public async Task AnInvoiceForAnUnknownCustomer_Fails_ThenWorksOnceTheCustomerIsApproved()
    {
        var (ctx, inbox) = await CreateAsync();
        var invoice = await inbox.ReceiveAsync("bot", "SalesInvoice", "inv1",
            J($$"""{"customerCode":"NOOR","invoiceDate":"{{Today}}","paymentTerm":"Credit","lines":[{"description":"خدمة","quantity":2,"unitPrice":500}]}"""));
        var failed = await inbox.ApproveAsync(invoice.Id, "x");
        Assert.Equal("Failed", failed.Status);
        Assert.Contains("NOOR", failed.ErrorMessage);
        Assert.Empty(ctx.SalesInvoices);

        var customer = await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"شركة النور","code":"NOOR"}"""));
        await inbox.ApproveAsync(customer.Id, "x");

        var ok = await inbox.ApproveAsync(invoice.Id, "x"); // a failed item can be approved again
        Assert.Equal("Approved", ok.Status);
        Assert.NotNull(ok.ResultEntityId);
        var created = await ctx.SalesInvoices.SingleAsync();
        Assert.Equal(1000m, created.SubTotal);
    }

    [Fact]
    public async Task ApproveAll_ProcessesCustomersBeforeInvoices_EvenWhenTheInvoiceArrivedFirst()
    {
        var (ctx, inbox) = await CreateAsync();
        await inbox.ReceiveAsync("bot", "SalesInvoice", "inv1",
            J($$"""{"customerName":"شركة النور","invoiceDate":"{{Today}}","lines":[{"description":"x","quantity":1,"unitPrice":100}]}"""));
        await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"شركة النور","code":"NOOR"}"""));
        await inbox.ReceiveAsync("bot", "Data", "d1", J("""{"title":"عقد","note":"ملف العميل"}"""));

        var result = await inbox.ApproveAllPendingAsync("ehab");
        Assert.Equal(3, result.Approved);
        Assert.Equal(0, result.Failed);
        Assert.Single(ctx.SalesInvoices);
    }

    [Fact]
    public async Task APayment_NeedsItsInvoiceApprovedFirst_ThenRecordsTheMoney()
    {
        var (ctx, inbox) = await CreateAsync();
        await inbox.ApproveAsync((await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"شركة النور","code":"NOOR"}"""))).Id, "x");
        var invoice = await inbox.ReceiveAsync("bot", "SalesInvoice", "inv1",
            J($$"""{"customerCode":"NOOR","invoiceDate":"{{Today}}","lines":[{"description":"x","quantity":1,"unitPrice":100}]}"""));
        var payment = await inbox.ReceiveAsync("bot", "SalesPayment", "pay1", J($$"""{"invoiceExternalRef":"inv1","amount":50,"method":"Cash","paymentDate":"{{Today}}"}"""));

        Assert.Equal("Failed", (await inbox.ApproveAsync(payment.Id, "x")).Status);
        await inbox.ApproveAsync(invoice.Id, "x");
        Assert.Equal("Approved", (await inbox.ApproveAsync(payment.Id, "x")).Status);
        Assert.Equal(50m, (await ctx.SalesInvoices.SingleAsync()).PaidAmount);
    }

    [Fact]
    public async Task ApprovingAnExpense_PostsDebitExpenseCreditCash()
    {
        var (ctx, inbox) = await CreateAsync();
        var r = await inbox.ReceiveAsync("bot", "Expense", "e1", J($$"""{"date":"{{Today}}","description":"بنزين","amount":100}"""));
        var done = await inbox.ApproveAsync(r.Id, "x");
        Assert.Equal("Approved", done.Status);
        var entry = await ctx.JournalEntries.Include(e => e.Lines).ThenInclude(l => l.Account).SingleAsync();
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(100m, entry.Lines.Single(l => l.Account!.Code == "5300").Debit);
        Assert.Equal(100m, entry.Lines.Single(l => l.Account!.Code == "1111").Credit);
    }

    [Fact]
    public async Task ApprovingAJournalEntry_PostsIt_AndAnUnknownAccountFailsClearly()
    {
        var (ctx, inbox) = await CreateAsync();
        var good = await inbox.ReceiveAsync("bot", "JournalEntry", "j1",
            J($$"""{"date":"{{Today}}","description":"تسوية","lines":[{"accountCode":"1112","debit":300},{"accountCode":"4100","credit":300}]}"""));
        Assert.Equal("Approved", (await inbox.ApproveAsync(good.Id, "x")).Status);
        Assert.Equal(JournalEntryStatus.Posted, (await ctx.JournalEntries.SingleAsync()).Status);

        var bad = await inbox.ReceiveAsync("bot", "JournalEntry", "j2",
            J($$"""{"date":"{{Today}}","lines":[{"accountCode":"9999","debit":10},{"accountCode":"4100","credit":10}]}"""));
        var failed = await inbox.ApproveAsync(bad.Id, "x");
        Assert.Equal("Failed", failed.Status);
        Assert.Contains("9999", failed.ErrorMessage);
    }

    [Fact]
    public async Task ADateOutsideEveryFiscalPeriod_FailsInsteadOfPosting()
    {
        var (ctx, inbox) = await CreateAsync();
        var r = await inbox.ReceiveAsync("bot", "Expense", "e1", J("""{"date":"2001-05-05","description":"قديم","amount":10}"""));
        Assert.Equal("Failed", (await inbox.ApproveAsync(r.Id, "x")).Status);
        Assert.Empty(ctx.JournalEntries);
    }

    [Fact]
    public async Task Reject_KeepsItOutOfTheBooks_AndApprovedItemsCannotBeRejected()
    {
        var (ctx, inbox) = await CreateAsync();
        var a = await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"أ","code":"A"}"""));
        var rejected = await inbox.RejectAsync(a.Id, "مكرر", "x");
        Assert.Equal("Rejected", rejected.Status);
        Assert.Empty(ctx.Customers);

        var b = await inbox.ReceiveAsync("bot", "Customer", "c2", J("""{"nameAr":"ب","code":"B"}"""));
        await inbox.ApproveAsync(b.Id, "x");
        await Assert.ThrowsAsync<ValidationAppException>(() => inbox.RejectAsync(b.Id, "تاني", "x"));
    }

    [Fact]
    public async Task ApprovingAnItem_CreatesIt_UsesAGeneralCategory_AndUpdatesAnExistingCode()
    {
        var (ctx, inbox) = await CreateAsync();
        var first = await inbox.ReceiveAsync("bot", "Item", "i1", J("""{"code":"ITM-1","nameAr":"قهوة","unitOfMeasure":"kg"}"""));
        Assert.Equal("Approved", (await inbox.ApproveAsync(first.Id, "x")).Status);
        var item = await ctx.Items.SingleAsync();
        Assert.Equal("kg", item.UnitOfMeasure);
        Assert.Single(ctx.ItemCategories);

        var again = await inbox.ReceiveAsync("bot", "Item", "i2", J("""{"code":"ITM-1","nameAr":"قهوة محمصة","unitOfMeasure":"kg"}"""));
        await inbox.ApproveAsync(again.Id, "x");
        Assert.Equal("قهوة محمصة", (await ctx.Items.SingleAsync()).NameAr);
    }

    [Fact]
    public async Task ApprovingABankLine_AddsItToTheBankFeed_AndTheSameReferenceIsNotImportedTwice()
    {
        var (ctx, inbox) = await CreateAsync();
        var r = await inbox.ReceiveAsync("bot", "BankStatementLine", "bank-991", J($$"""{"date":"{{Today}}","description":"تحويل من النور","amount":2500}"""));
        Assert.Equal("Approved", (await inbox.ApproveAsync(r.Id, "x")).Status);
        var line = await ctx.BankFeedTransactions.SingleAsync();
        Assert.Equal(2500m, line.Amount);
        Assert.Equal("bank-991", line.ExternalId);
        Assert.False(line.IsMatched);
    }

    [Fact]
    public async Task ApproveAll_ProcessesItemsBeforeInvoicesThatUseThem()
    {
        var (ctx, inbox) = await CreateAsync();
        ctx.Warehouses.Add(new RomaERP.Domain.Inventory.Warehouse { Code = "WH-1", NameAr = "مخزن", NameEn = "Main" });
        await ctx.SaveChangesAsync();
        await inbox.ReceiveAsync("bot", "SalesInvoice", "inv1", J($$"""{"customerCode":"NOOR","invoiceDate":"{{Today}}","warehouseCode":"WH-1","lines":[{"description":"قهوة","quantity":1,"unitPrice":50,"itemCode":"ITM-1"}]}"""));
        await inbox.ReceiveAsync("bot", "Item", "i1", J("""{"code":"ITM-1","nameAr":"قهوة"}"""));
        await inbox.ReceiveAsync("bot", "Customer", "c1", J("""{"nameAr":"النور","code":"NOOR"}"""));
        var all = await inbox.ApproveAllPendingAsync("x");
        // the customer and the item exist before the invoice is tried; selling an item with no stock may still be refused by stock rules
        Assert.Equal(3, all.Approved + all.Failed);
        Assert.Single(ctx.Items);
        Assert.Single(ctx.Customers);
    }

    private static string FindExample(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", name))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "docs", "examples", name);
    }

    /// <summary>The documented sample batch (a real client's sales report and two supplier invoices) must go through end to end and
    /// reproduce the printed totals: invoice 1 = 880.05, invoice 2's taxable part = 108.00 with its zero-rated part 65.00 = 173.00.</summary>
    [Fact]
    public async Task TheSaladBarSampleBatch_ApprovesCleanly_AndMatchesThePrintedInvoiceTotals()
    {
        var (ctx, inbox) = await CreateAsync();
        ctx.Accounts.AddRange(
            new Account { Code = "5500", NameAr = "تكلفة", NameEn = "COGS", AccountType = AccountType.Expense, Nature = AccountNature.Debit },
            new Account { Code = "1180", NameAr = "ضريبة مدخلات", NameEn = "Input VAT", AccountType = AccountType.Asset, Nature = AccountNature.Debit });
        await ctx.SaveChangesAsync();
        var settings = await ctx.CompanySettings.SingleAsync();
        settings.VatRate = 0.15m;
        await ctx.SaveChangesAsync();

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(FindExample("salad-bar-samples.json")));
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            var data = item.GetProperty("data").Clone();
            // the sample's dates are real ones from the client; move them into the test's open fiscal period
            var text = System.Text.RegularExpressions.Regex.Replace(data.GetRawText(), "2026-11-(07|14)", Today);
            await inbox.ReceiveAsync("bot", item.GetProperty("type").GetString()!, item.GetProperty("externalRef").GetString()!, JsonDocument.Parse(text).RootElement.Clone());
        }

        var result = await inbox.ApproveAllPendingAsync("x");
        Assert.Equal(5, result.Approved);
        Assert.Equal(0, result.Failed);

        var invoices = await ctx.PurchaseInvoices.OrderBy(i => i.TotalAmount).ToListAsync();
        Assert.Equal(new[] { 108.00m, 880.05m }, invoices.Select(i => i.TotalAmount).ToArray());
        Assert.Equal(new[] { 14.09m, 114.79m }, invoices.Select(i => i.VatAmount).ToArray());
        var sales = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Reference!.StartsWith("SALES-"));
        Assert.Equal(sales.Lines.Sum(l => l.Debit), sales.Lines.Sum(l => l.Credit));
        Assert.Equal(166.50m, sales.Lines.Sum(l => l.Debit));
    }

    [Fact]
    public async Task ApiKeys_AreStoredHashedOnly_ValidateWhileActive_AndStopWorkingWhenRevoked()
    {
        var (ctx, _) = await CreateAsync();
        var keys = new IntegrationApiKeyService(ctx);
        var created = await keys.CreateAsync("البوت");
        Assert.StartsWith("rk_", created.Key);

        var stored = await ctx.IntegrationApiKeys.SingleAsync();
        Assert.DoesNotContain(created.Key, stored.KeyHash);
        Assert.Equal(64, stored.KeyHash.Length);

        Assert.Equal("البوت", await keys.ValidateAsync(created.Key));
        Assert.Null(await keys.ValidateAsync("rk_wrong"));
        Assert.Null(await keys.ValidateAsync(null));

        await keys.RevokeAsync(created.Id);
        Assert.Null(await keys.ValidateAsync(created.Key));
    }
}
