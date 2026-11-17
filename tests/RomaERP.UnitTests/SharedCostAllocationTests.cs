using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Inventory;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class SharedCostAllocationTests
{
    private record S(ApplicationDbContext Ctx, Warehouse Cairo, Warehouse Alex, Account Rent, Account Payroll, Account Sales, Account Cash, FiscalPeriod Period);

    private static async Task<S> CreateAsync(bool withSales = true)
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cairo = new Warehouse { Code = "CAI", NameAr = "القاهرة", NameEn = "Cairo" };
        var alex = new Warehouse { Code = "ALX", NameAr = "اسكندرية", NameEn = "Alex" };
        var rent = new Account { Code = "5200", NameAr = "إيجارات", NameEn = "Rent", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var payroll = new Account { Code = "5100", NameAr = "مرتبات", NameEn = "Payroll", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var sales = new Account { Code = "4100", NameAr = "مبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "Nov", PeriodNumber = 11, StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };
        ctx.AddRange(cairo, alex, rent, payroll, sales, cash, year, period);
        await ctx.SaveChangesAsync();

        JournalEntry E(string n, Guid? w, params (Account a, decimal d, decimal c)[] ls) => new()
        {
            EntryNumber = n, EntryDate = new DateTime(2026, 11, 10), FiscalPeriodId = period.Id, Status = JournalEntryStatus.Posted, WarehouseId = w,
            Lines = ls.Select((l, i) => new JournalEntryLine { LineNumber = i + 1, AccountId = l.a.Id, Debit = l.d, Credit = l.c }).ToList()
        };
        if (withSales)
        {
            ctx.JournalEntries.Add(E("JV-1", cairo.Id, (cash, 1000, 0), (sales, 0, 1000)));
            ctx.JournalEntries.Add(E("JV-2", alex.Id, (cash, 500, 0), (sales, 0, 500)));
        }
        ctx.JournalEntries.Add(E("JV-3", null, (rent, 900, 0), (cash, 0, 900)));          // head office rent
        ctx.JournalEntries.Add(E("JV-4", null, (payroll, 600, 0), (cash, 0, 600)));       // head office payroll
        await ctx.SaveChangesAsync();
        return new S(ctx, cairo, alex, rent, payroll, sales, cash, period);
    }

    [Fact]
    public async Task Preview_ProposesTheSharedCostsOnEachBranchsShareOfSales()
    {
        var s = await CreateAsync();
        var p = await new SharedCostAllocationService(s.Ctx).PreviewAsync(2026, 11);
        Assert.Null(p.Blocked);
        Assert.Equal(1500m, p.TotalToAllocate);
        Assert.Equal(66.67m, p.Branches.Single(b => b.WarehouseName == "القاهرة").SharePercent);
        Assert.Equal(33.33m, p.Branches.Single(b => b.WarehouseName == "اسكندرية").SharePercent);
        Assert.Equal(100m, p.Branches.Sum(b => b.SharePercent));
    }

    [Fact]
    public async Task Apply_MovesTheCostsToTheBranches_InOneEntry_WithoutChangingAnyAccountTotal()
    {
        var s = await CreateAsync();
        var svc = new SharedCostAllocationService(s.Ctx);
        var id = await svc.ApplyAsync(new ApplyCostAllocationDto { Year = 2026, Month = 11 }, "accountant");

        var entry = await s.Ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == id);
        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        Assert.Equal(new DateTime(2026, 11, 30), entry.EntryDate);
        Assert.Equal(0m, entry.Lines.Where(l => l.AccountId == s.Rent.Id).Sum(l => l.Debit - l.Credit));   // rent in total unchanged
        Assert.Equal(600m, entry.Lines.Single(l => l.AccountId == s.Rent.Id && l.WarehouseId == s.Cairo.Id).Debit);    // 900 x 2/3
        Assert.Equal(300m, entry.Lines.Single(l => l.AccountId == s.Rent.Id && l.WarehouseId == s.Alex.Id).Debit);

        // nothing left to allocate, so running it again is refused
        var again = await svc.PreviewAsync(2026, 11);
        Assert.NotNull(again.Blocked);
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.ApplyAsync(new ApplyCostAllocationDto { Year = 2026, Month = 11 }, "accountant"));
    }

    [Fact]
    public async Task TheAccountantCanChangeTheShares_ButTheyMustTotal100()
    {
        var s = await CreateAsync();
        var svc = new SharedCostAllocationService(s.Ctx);
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.ApplyAsync(new ApplyCostAllocationDto { Year = 2026, Month = 11, Shares = { new BranchShareDto { WarehouseId = s.Cairo.Id, SharePercent = 60 }, new BranchShareDto { WarehouseId = s.Alex.Id, SharePercent = 30 } } }, "a"));
        var id = await svc.ApplyAsync(new ApplyCostAllocationDto { Year = 2026, Month = 11, Shares = { new BranchShareDto { WarehouseId = s.Cairo.Id, SharePercent = 50 }, new BranchShareDto { WarehouseId = s.Alex.Id, SharePercent = 50 } } }, "a");
        var entry = await s.Ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == id);
        Assert.Equal(450m, entry.Lines.Single(l => l.AccountId == s.Rent.Id && l.WarehouseId == s.Cairo.Id).Debit);
    }

    [Fact]
    public async Task WithoutBranchSales_ThereIsNoBasis_AndNothingIsPosted()
    {
        var s = await CreateAsync(withSales: false);
        var svc = new SharedCostAllocationService(s.Ctx);
        Assert.NotNull((await svc.PreviewAsync(2026, 11)).Blocked);
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.ApplyAsync(new ApplyCostAllocationDto { Year = 2026, Month = 11 }, "a"));
    }

    [Fact]
    public async Task TheBranchReport_ShowsAllocatedCostsAsSharedCosts_NotAsTheBranchsOwnSpending_AndTotalsStayTheSame()
    {
        var s = await CreateAsync();
        var reports = new FinancialReportService(s.Ctx);
        var before = await reports.GetBranchProfitAsync(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));

        await new SharedCostAllocationService(s.Ctx).ApplyAsync(new ApplyCostAllocationDto { Year = 2026, Month = 11 }, "a");
        var after = await reports.GetBranchProfitAsync(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));

        var cairo = after.Branches.Single(b => b.BranchName == "القاهرة");
        Assert.Equal(1000m, cairo.Contribution);                  // stage 1 untouched by the allocation entry
        Assert.Equal(0m, cairo.OtherDirectExpenses);
        Assert.Equal(1000m, cairo.SharedAllocated);               // 1500 x 2/3, exactly as the report worked it out before
        Assert.Equal(before.NetProfit, after.NetProfit);          // company total unchanged
        Assert.DoesNotContain(after.Branches, b => b.WarehouseId == null);   // nothing left on head office
        Assert.Equal(before.Branches.Single(b => b.BranchName == "القاهرة").SharedAllocated, cairo.SharedAllocated);   // same figure the report already produced on the fly
    }
}
