using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Sales;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class ReviewServiceTests
{
    private record S(ApplicationDbContext Ctx, ReviewService Svc, Account Cash, Account Ar, Account Revenue, Account Capital, FiscalPeriod Period);

    private static async Task<S> CreateAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ar = new Account { Code = "1120", NameAr = "العملاء", NameEn = "AR", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var rev = new Account { Code = "4100", NameAr = "مبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var cap = new Account { Code = "3100", NameAr = "رأس المال", NameEn = "Capital", AccountType = AccountType.Equity, Nature = AccountNature.Credit };
        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Nov", PeriodNumber = 11, StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        ctx.AddRange(cash, ar, rev, cap, year, period);
        await ctx.SaveChangesAsync();
        return new S(ctx, new ReviewService(ctx, new SharedCostAllocationService(ctx)), cash, ar, rev, cap, period);
    }

    private static JournalEntry Entry(S s, string number, DateTime date, Account debit, Account credit, decimal amount, string? description = null, JournalEntryStatus status = JournalEntryStatus.Posted, string? reference = null) => new()
    {
        EntryNumber = number, EntryDate = date, FiscalPeriodId = s.Period.Id, Status = status, Description = description, Reference = reference,
        Lines = { new JournalEntryLine { LineNumber = 1, AccountId = debit.Id, Debit = amount }, new JournalEntryLine { LineNumber = 2, AccountId = credit.Id, Credit = amount } }
    };

    [Fact]
    public async Task Reconciliation_FlagsASubledgerThatDisagreesWithTheLedger()
    {
        var s = await CreateAsync();
        s.Ctx.JournalEntries.Add(Entry(s, "JV-000001", new DateTime(2026, 11, 5), s.Ar, s.Revenue, 500m));
        s.Ctx.Customers.Add(new Customer { Code = "C1", NameAr = "عميل", NameEn = "C", ArBalance = 500m });
        await s.Ctx.SaveChangesAsync();
        var ok = await s.Svc.GetReconciliationAsync();
        Assert.True(ok.Single(c => c.Key == "ar").Ok);
        Assert.True(ok.Single(c => c.Key == "trial").Ok);

        s.Ctx.Customers.Single().ArBalance = 450m;
        await s.Ctx.SaveChangesAsync();
        var bad = (await s.Svc.GetReconciliationAsync()).Single(c => c.Key == "ar");
        Assert.False(bad.Ok);
        Assert.Equal(50m, bad.Difference);
    }

    [Fact]
    public async Task Reconciliation_CountsAReversedEntryAndItsReversalTogether_SoTheyNetOut()
    {
        var s = await CreateAsync();
        s.Ctx.JournalEntries.Add(Entry(s, "JV-000001", new DateTime(2026, 11, 5), s.Ar, s.Revenue, 500m, status: JournalEntryStatus.Reversed));
        s.Ctx.JournalEntries.Add(Entry(s, "JV-000002", new DateTime(2026, 11, 6), s.Revenue, s.Ar, 500m, "عكس قيد رقم JV-000001"));
        await s.Ctx.SaveChangesAsync();
        Assert.True((await s.Svc.GetReconciliationAsync()).Single(c => c.Key == "ar").Ok);   // 0 in the books, 0 owed
    }

    [Fact]
    public async Task CloseChecklist_ListsWhatIsStillOpen()
    {
        var s = await CreateAsync();
        s.Ctx.JournalEntries.Add(Entry(s, "JV-000001", new DateTime(2026, 11, 5), s.Cash, s.Revenue, 100m, status: JournalEntryStatus.Draft));
        await s.Ctx.SaveChangesAsync();
        var list = await s.Svc.GetCloseChecklistAsync(2026, 11);
        Assert.False(list.Single(c => c.Key == "drafts").Ok);
        Assert.Equal(1, list.Single(c => c.Key == "drafts").Count);
        Assert.True(list.Single(c => c.Key == "counts").Ok);
        Assert.True(list.Single(c => c.Key == "trialBalanced").Ok);
    }

    [Fact]
    public async Task UnusualEntries_FlagsRoundBackdatedReversalAndEquity_AndLeavesOrdinaryOnesAlone()
    {
        var s = await CreateAsync();
        var created = DateTime.UtcNow;
        var ordinary = Entry(s, "JV-000001", DateTime.UtcNow.Date.AddDays(-2), s.Cash, s.Revenue, 1234.56m);
        var round = Entry(s, "JV-000002", DateTime.UtcNow.Date.AddDays(-2), s.Cash, s.Revenue, 50000m);
        var equity = Entry(s, "JV-000003", DateTime.UtcNow.Date.AddDays(-2), s.Cash, s.Capital, 777m);
        var old = Entry(s, "JV-000004", DateTime.UtcNow.Date.AddDays(-60), s.Cash, s.Revenue, 321m);
        old.CreatedAtUtc = created;   // entered today for a date two months ago
        var reversal = Entry(s, "JV-000005", DateTime.UtcNow.Date.AddDays(-1), s.Revenue, s.Cash, 99m, "عكس قيد رقم JV-000004");
        s.Ctx.JournalEntries.AddRange(ordinary, round, equity, old, reversal);
        await s.Ctx.SaveChangesAsync();

        var flagged = await s.Svc.GetUnusualEntriesAsync(null, null);
        Assert.DoesNotContain(flagged, f => f.EntryNumber == "JV-000001");
        Assert.Contains("Round", flagged.Single(f => f.EntryNumber == "JV-000002").Reasons);
        Assert.Contains("Equity", flagged.Single(f => f.EntryNumber == "JV-000003").Reasons);
        Assert.Contains("Backdated", flagged.Single(f => f.EntryNumber == "JV-000004").Reasons);
        Assert.Contains("Reversal", flagged.Single(f => f.EntryNumber == "JV-000005").Reasons);
    }

    [Fact]
    public async Task NumberGaps_ShowsTheMissingNumbersInASequence()
    {
        var s = await CreateAsync();
        foreach (var n in new[] { 1, 2, 4, 7 }) s.Ctx.JournalEntries.Add(Entry(s, $"JV-{n:D6}", new DateTime(2026, 11, 5), s.Cash, s.Revenue, 10m));
        await s.Ctx.SaveChangesAsync();
        var gap = (await s.Svc.GetNumberGapsAsync()).Single(g => g.Series.StartsWith("JournalEntry"));
        Assert.Equal(4, gap.Count);
        Assert.Equal(new long[] { 3, 5, 6 }, gap.Missing.ToArray());
        Assert.Equal(3, gap.MissingCount);
    }

    [Fact]
    public async Task ApprovalsLog_ListsDecidedCountsWithWhoAndWhen()
    {
        var s = await CreateAsync();
        var cat = new ItemCategory { Code = "C", NameAr = "خامات", NameEn = "Raw" };
        var wh = new Warehouse { Code = "CAI", NameAr = "القاهرة", NameEn = "Cairo" };
        var item = new Item { Code = "TOM", NameAr = "طماطم", NameEn = "Tomato", UnitOfMeasure = "kg", ItemCategory = cat, AverageCost = 10m };
        s.Ctx.AddRange(cat, wh, item);
        await s.Ctx.SaveChangesAsync();
        s.Ctx.PhysicalStockCounts.Add(new PhysicalStockCount { ItemId = item.Id, WarehouseId = wh.Id, CountDate = DateTime.UtcNow.Date, SystemQuantity = 20, CountedQuantity = 18, UnitCost = 10m, Status = 2, DecidedBy = "المالك", DecidedAt = DateTime.UtcNow });
        await s.Ctx.SaveChangesAsync();
        var log = await s.Svc.GetApprovalsLogAsync();
        var row = Assert.Single(log);
        Assert.Equal("StockCount", row.Kind);
        Assert.Equal("المالك", row.By);
        Assert.Equal(-20m, row.Amount);
    }
}
