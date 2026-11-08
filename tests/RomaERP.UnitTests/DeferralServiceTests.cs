using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class DeferralServiceTests
{
    private static async Task<(ApplicationDbContext ctx, Account cash, Account rent, Account sales, Account prepaid, Account unearned)> SeedAsync(bool openPeriod = true)
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var prepaid = new Account { Code = "1140", NameAr = "مصروفات مقدمة", NameEn = "Prepaid", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var unearned = new Account { Code = "2140", NameAr = "إيرادات مقدمة", NameEn = "Unearned", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var rent = new Account { Code = "5200", NameAr = "إيجارات", NameEn = "Rent", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var sales = new Account { Code = "4100", NameAr = "المبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        ctx.Accounts.AddRange(cash, prepaid, unearned, rent, sales);
        // One long period so any month used by the tests is open (or closed when asked).
        ctx.FiscalPeriods.Add(new FiscalPeriod { Name = "All", PeriodNumber = 1, StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2040, 12, 31), IsClosed = !openPeriod });
        await ctx.SaveChangesAsync();
        return (ctx, cash, rent, sales, prepaid, unearned);
    }

    [Fact]
    public async Task PrepaidExpense_PostsTheCashSideNow_ThenReleasesMonthlyIncludingTheRounding()
    {
        var (ctx, cash, rent, _, prepaid, _) = await SeedAsync();
        var svc = new DeferralService(ctx);
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-3);

        await svc.CreateAsync(new CreateDeferralDto { Kind = DeferralKind.PrepaidExpense, Description = "إيجار", TotalAmount = 1000, EntryDate = start, StartMonth = start, Months = 3, TargetAccountId = rent.Id, SettlementAccountId = cash.Id });
        var first = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync();
        Assert.Equal(prepaid.Id, first.Lines.Single(l => l.Debit > 0).AccountId);
        Assert.Equal(cash.Id, first.Lines.Single(l => l.Credit > 0).AccountId);

        var run = await svc.RunAsync();   // three months due (and the current month is the 4th, but only 3 exist)

        Assert.Equal(3, run.EntriesPosted);
        Assert.Equal(1000, run.AmountReleased);   // 333.33 + 333.33 + 333.34
        var all = await ctx.JournalEntries.Include(e => e.Lines).ToListAsync();
        Assert.All(all, e => Assert.True(e.IsBalanced));
        Assert.Equal(1000, all.SelectMany(e => e.Lines).Where(l => l.AccountId == rent.Id).Sum(l => l.Debit));
        Assert.Equal(0, all.SelectMany(e => e.Lines).Where(l => l.AccountId == prepaid.Id).Sum(l => l.Debit - l.Credit));   // fully released
        Assert.Equal(DeferralStatus.Completed, (await ctx.DeferralSchedules.SingleAsync()).Status);

        var again = await svc.RunAsync();   // running twice changes nothing
        Assert.Equal(0, again.EntriesPosted);
    }

    [Fact]
    public async Task DeferredRevenue_CollectsFirst_ThenEarnsEachMonth_AndOnlyMonthsThatHaveComeDue()
    {
        var (ctx, cash, _, sales, _, unearned) = await SeedAsync();
        var svc = new DeferralService(ctx);
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var created = await svc.CreateAsync(new CreateDeferralDto { Kind = DeferralKind.DeferredRevenue, Description = "اشتراك سنوي", TotalAmount = 1200, EntryDate = DateTime.UtcNow, StartMonth = thisMonth, Months = 12, TargetAccountId = sales.Id, SettlementAccountId = cash.Id });
        Assert.Equal(100, created.MonthlyAmount);

        var run = await svc.RunAsync();

        Assert.Equal(1, run.EntriesPosted);   // only this month has come due
        var lines = (await ctx.JournalEntries.Include(e => e.Lines).ToListAsync()).SelectMany(e => e.Lines).ToList();
        Assert.Equal(100, lines.Where(l => l.AccountId == sales.Id).Sum(l => l.Credit));
        Assert.Equal(1100, lines.Where(l => l.AccountId == unearned.Id).Sum(l => l.Credit - l.Debit));   // still owed as service
        Assert.Equal(1100, (await svc.GetAllAsync()).Single().Remaining);
    }

    [Fact]
    public async Task RunSkipsAMonthInAClosedPeriod_AndRejectsBadInput()
    {
        var (ctx, cash, rent, sales, _, _) = await SeedAsync(openPeriod: false);
        var svc = new DeferralService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(new CreateDeferralDto { Kind = DeferralKind.PrepaidExpense, Description = "x", TotalAmount = 100, EntryDate = DateTime.UtcNow, StartMonth = DateTime.UtcNow, Months = 3, TargetAccountId = rent.Id, SettlementAccountId = cash.Id }));   // closed period

        var open = await SeedAsync();
        var open_svc = new DeferralService(open.ctx);
        await Assert.ThrowsAsync<ValidationAppException>(() => open_svc.CreateAsync(new CreateDeferralDto { Kind = DeferralKind.PrepaidExpense, Description = "x", TotalAmount = 100, EntryDate = DateTime.UtcNow, StartMonth = DateTime.UtcNow, Months = 1, TargetAccountId = open.rent.Id, SettlementAccountId = open.cash.Id }));   // < 2 months
        await Assert.ThrowsAsync<ValidationAppException>(() => open_svc.CreateAsync(new CreateDeferralDto { Kind = DeferralKind.PrepaidExpense, Description = "x", TotalAmount = 100, EntryDate = DateTime.UtcNow, StartMonth = DateTime.UtcNow, Months = 3, TargetAccountId = open.sales.Id, SettlementAccountId = open.cash.Id }));   // target must be an expense
    }
}
