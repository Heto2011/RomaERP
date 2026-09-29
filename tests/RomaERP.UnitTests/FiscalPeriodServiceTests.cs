using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class FiscalPeriodServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account cash, Account sales, Account rent, Account retainedEarnings, FiscalYear year, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var sales = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var rent = new Account { Code = "5200", NameAr = "إيجارات", NameEn = "Rent", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var retainedEarnings = new Account { Code = "3200", NameAr = "أرباح مرحلة", NameEn = "Retained Earnings", AccountType = AccountType.Equity, Nature = AccountNature.Credit };

        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "January", PeriodNumber = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) };
        year.Periods.Add(period);

        ctx.Accounts.AddRange(cash, sales, rent, retainedEarnings);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        return (ctx, cash, sales, rent, retainedEarnings, year, period);
    }

    [Fact]
    public async Task CloseFiscalYear_WithRevenueAndExpense_ZeroesThemAndCreditsRetainedEarnings()
    {
        var (ctx, cash, sales, rent, retainedEarnings, year, period) = await SeedAsync();
        var journalService = new JournalEntryService(ctx);

        var revenueEntry = await journalService.CreateAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 10),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 15000, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = sales.Id, Debit = 0, Credit = 15000 }
            }
        });
        await journalService.PostAsync(revenueEntry.Id);

        var expenseEntry = await journalService.CreateAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 12),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = rent.Id, Debit = 4000, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 0, Credit = 4000 }
            }
        });
        await journalService.PostAsync(expenseEntry.Id);

        var periodService = new FiscalPeriodService(ctx);
        await periodService.ClosePeriodAsync(period.Id);
        await periodService.CloseFiscalYearAsync(year.Id);

        var trialBalance = await journalService.GetTrialBalanceAsync(null);

        Assert.Equal(0, trialBalance.Single(l => l.AccountCode == "4100").Balance);
        Assert.Equal(0, trialBalance.Single(l => l.AccountCode == "5200").Balance);
        Assert.Equal(11000, trialBalance.Single(l => l.AccountCode == "3200").Balance);
    }

    [Fact]
    public async Task ClosePeriod_WithDraftEntry_ThrowsValidationException()
    {
        var (ctx, cash, sales, _, _, _, period) = await SeedAsync();
        var journalService = new JournalEntryService(ctx);

        await journalService.CreateAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 10),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 100, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = sales.Id, Debit = 0, Credit = 100 }
            }
        });

        var periodService = new FiscalPeriodService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => periodService.ClosePeriodAsync(period.Id));
    }

    [Fact]
    public async Task CloseFiscalYear_WithOpenPeriods_ThrowsValidationException()
    {
        var (ctx, _, _, _, _, year, _) = await SeedAsync();
        var periodService = new FiscalPeriodService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => periodService.CloseFiscalYearAsync(year.Id));
    }
}
