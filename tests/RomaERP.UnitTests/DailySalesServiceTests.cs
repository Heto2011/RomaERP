using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class DailySalesServiceTests
{
    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        ctx.Accounts.AddRange(
            new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = "1112", NameAr = "البنك", NameEn = "Bank", AccountType = AccountType.Asset, Nature = AccountNature.Debit },
            new Account { Code = "4100", NameAr = "المبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit },
            new Account { Code = "2161", NameAr = "ضريبة", NameEn = "VAT", AccountType = AccountType.Liability, Nature = AccountNature.Credit });
        ctx.FiscalPeriods.Add(new FiscalPeriod { Name = "Nov", PeriodNumber = 11, StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Splits_vat_out_of_the_inclusive_total_and_balances()
    {
        var ctx = await SeedAsync();
        var svc = new DailySalesService(ctx);

        var r = await svc.CreateAsync(new CreateDailySalesDto { SalesDate = new DateTime(2026, 11, 7), CashAmount = 800, CardAmount = 342, VatRatePercent = 14 });

        Assert.Equal(1142, r.Total);
        Assert.Equal(1001.75m, r.NetSales);
        Assert.Equal(140.25m, r.VatAmount);
        var entry = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync();
        Assert.True(entry.IsBalanced);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
    }

    [Fact]
    public async Task Rejects_a_duplicate_for_the_same_day_but_allows_another_label()
    {
        var svc = new DailySalesService(await SeedAsync());
        var day = new DateTime(2026, 11, 7);
        await svc.CreateAsync(new CreateDailySalesDto { SalesDate = day, CashAmount = 100 });

        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(new CreateDailySalesDto { SalesDate = day, CashAmount = 50 }));
        await svc.CreateAsync(new CreateDailySalesDto { SalesDate = day, CashAmount = 50, Label = "فرع 2" });

        var recent = await svc.GetRecentAsync(10);
        Assert.Equal(2, recent.Count);
        Assert.Contains(recent, x => x.Label == "فرع 2");
    }

    [Fact]
    public async Task Rejects_empty_totals_and_dates_outside_an_open_period()
    {
        var svc = new DailySalesService(await SeedAsync());
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(new CreateDailySalesDto { SalesDate = new DateTime(2026, 11, 7) }));
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.CreateAsync(new CreateDailySalesDto { SalesDate = new DateTime(2027, 5, 1), CashAmount = 10 }));
    }
}
