using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class JournalEntryServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account cash, Account capital, FiscalPeriod period)> SeedAsync()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var capital = new Account { Code = "3100", NameAr = "رأس المال", NameEn = "Capital", AccountType = AccountType.Equity, Nature = AccountNature.Credit };
        var fiscalYear = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = fiscalYear, FiscalYearId = fiscalYear.Id, Name = "January", PeriodNumber = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) };

        ctx.Accounts.AddRange(cash, capital);
        ctx.FiscalYears.Add(fiscalYear);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        return (ctx, cash, capital, period);
    }

    [Fact]
    public async Task CreateAsync_WithBalancedLines_CreatesDraftEntry()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new JournalEntryService(ctx);

        var dto = new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 5),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 1000, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = capital.Id, Debit = 0, Credit = 1000 }
            }
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(JournalEntryStatus.Draft, result.Status);
        Assert.Equal(1000, result.TotalDebit);
        Assert.Equal(1000, result.TotalCredit);
    }

    [Fact]
    public async Task CreateAsync_WithUnbalancedLines_ThrowsValidationException()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new JournalEntryService(ctx);

        var dto = new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 5),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 1000, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = capital.Id, Debit = 0, Credit = 500 }
            }
        };

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task PostAsync_TwiceOnSameEntry_ThrowsValidationException()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new JournalEntryService(ctx);

        var created = await service.CreateAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 5),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 500, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = capital.Id, Debit = 0, Credit = 500 }
            }
        });

        await service.PostAsync(created.Id);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.PostAsync(created.Id));
    }

    [Fact]
    public async Task GetTrialBalance_AfterPosting_ReflectsCorrectBalancesByNature()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new JournalEntryService(ctx);

        var created = await service.CreateAsync(new CreateJournalEntryDto
        {
            EntryDate = new DateTime(2026, 1, 5),
            FiscalPeriodId = period.Id,
            Lines =
            {
                new CreateJournalEntryLineDto { AccountId = cash.Id, Debit = 2000, Credit = 0 },
                new CreateJournalEntryLineDto { AccountId = capital.Id, Debit = 0, Credit = 2000 }
            }
        });
        await service.PostAsync(created.Id);

        var trialBalance = await service.GetTrialBalanceAsync(null);

        var cashLine = trialBalance.Single(l => l.AccountCode == "1111");
        var capitalLine = trialBalance.Single(l => l.AccountCode == "3100");

        Assert.Equal(2000, cashLine.Balance);
        Assert.Equal(2000, capitalLine.Balance);
    }
}
