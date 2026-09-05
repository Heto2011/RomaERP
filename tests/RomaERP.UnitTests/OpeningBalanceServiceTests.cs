using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class OpeningBalanceServiceTests
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
    public async Task CreateAsync_WithBalancedTrialBalance_CreatesPostedEntry()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new OpeningBalanceService(ctx);

        var result = await service.CreateAsync(new CreateOpeningBalanceDto
        {
            FiscalPeriodId = period.Id,
            EntryDate = period.StartDate,
            Lines =
            {
                new OpeningBalanceLineInputDto { AccountId = cash.Id, Debit = 75000, Credit = 0 },
                new OpeningBalanceLineInputDto { AccountId = capital.Id, Debit = 0, Credit = 75000 }
            }
        });

        Assert.Equal(JournalEntryStatus.Posted, result.Status);
        Assert.Equal(75000, result.TotalDebit);
        Assert.Equal(75000, result.TotalCredit);
        Assert.Equal("OPENING-BALANCE", result.Reference);
    }

    [Fact]
    public async Task CreateAsync_WithUnbalancedTrialBalance_ThrowsValidationException()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new OpeningBalanceService(ctx);

        var dto = new CreateOpeningBalanceDto
        {
            FiscalPeriodId = period.Id,
            EntryDate = period.StartDate,
            Lines =
            {
                new OpeningBalanceLineInputDto { AccountId = cash.Id, Debit = 75000, Credit = 0 },
                new OpeningBalanceLineInputDto { AccountId = capital.Id, Debit = 0, Credit = 70000 }
            }
        };

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_WhenOpeningBalanceAlreadyExistsForYear_ThrowsValidationException()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new OpeningBalanceService(ctx);

        var dto = new CreateOpeningBalanceDto
        {
            FiscalPeriodId = period.Id,
            EntryDate = period.StartDate,
            Lines =
            {
                new OpeningBalanceLineInputDto { AccountId = cash.Id, Debit = 75000, Credit = 0 },
                new OpeningBalanceLineInputDto { AccountId = capital.Id, Debit = 0, Credit = 75000 }
            }
        };

        await service.CreateAsync(dto);

        var secondAttempt = new CreateOpeningBalanceDto
        {
            FiscalPeriodId = period.Id,
            EntryDate = period.StartDate,
            Lines =
            {
                new OpeningBalanceLineInputDto { AccountId = cash.Id, Debit = 1000, Credit = 0 },
                new OpeningBalanceLineInputDto { AccountId = capital.Id, Debit = 0, Credit = 1000 }
            }
        };

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(secondAttempt));
    }

    [Fact]
    public async Task GetForFiscalYearAsync_AfterCreate_ReturnsTheEntry()
    {
        var (ctx, cash, capital, period) = await SeedAsync();
        var service = new OpeningBalanceService(ctx);

        await service.CreateAsync(new CreateOpeningBalanceDto
        {
            FiscalPeriodId = period.Id,
            EntryDate = period.StartDate,
            Lines =
            {
                new OpeningBalanceLineInputDto { AccountId = cash.Id, Debit = 5000, Credit = 0 },
                new OpeningBalanceLineInputDto { AccountId = capital.Id, Debit = 0, Credit = 5000 }
            }
        });

        var result = await service.GetForFiscalYearAsync(period.FiscalYearId);

        Assert.NotNull(result);
        Assert.Equal(5000, result!.TotalDebit);
    }
}
