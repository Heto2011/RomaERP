using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class BudgetServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationDbContext ctx, Account revenue, Account expense, Account cash, FiscalYear year, FiscalPeriod jan, FiscalPeriod feb)> SeedAsync()
    {
        var ctx = CreateContext();

        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var expense = new Account { Code = "5300", NameAr = "مصروفات إدارية", NameEn = "Admin Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };

        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var jan = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "January 2026", PeriodNumber = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) };
        var feb = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "February 2026", PeriodNumber = 2, StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2026, 2, 28) };
        year.Periods = new List<FiscalPeriod> { jan, feb };

        ctx.Accounts.AddRange(revenue, expense, cash);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.AddRange(jan, feb);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, VatRate = 0m, DefaultCurrency = "EGP" });
        await ctx.SaveChangesAsync();

        return (ctx, revenue, expense, cash, year, jan, feb);
    }

    [Fact]
    public async Task SetBudgetLineAsync_CreatesThenUpdatesTheSameLine()
    {
        var (ctx, revenue, _, _, _, jan, _) = await SeedAsync();
        var service = new BudgetService(ctx);

        var created = await service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = revenue.Id, FiscalPeriodId = jan.Id, Amount = 10000m });
        var updated = await service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = revenue.Id, FiscalPeriodId = jan.Id, Amount = 12000m });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(12000m, updated.Amount);
        Assert.Single(await ctx.Budgets.ToListAsync());
    }

    [Fact]
    public async Task SetBudgetLineAsync_RejectsControlAccount()
    {
        var ctx = CreateContext();
        var controlAccount = new Account { Code = "4000", NameAr = "الإيرادات", NameEn = "Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit, IsControlAccount = true };
        var period = new FiscalPeriod { Name = "January 2026", PeriodNumber = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) };
        ctx.Accounts.Add(controlAccount);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        var service = new BudgetService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = controlAccount.Id, FiscalPeriodId = period.Id, Amount = 1000m }));
    }

    [Fact]
    public async Task SetBudgetLineAsync_RejectsNonRevenueExpenseAccount()
    {
        var (ctx, _, _, cash, _, jan, _) = await SeedAsync();
        var service = new BudgetService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = cash.Id, FiscalPeriodId = jan.Id, Amount = 1000m }));
    }

    [Fact]
    public async Task GetBudgetVsActualAsync_ComputesActualsFromPostedEntriesAndVarianceAgainstBudget()
    {
        var (ctx, revenue, expense, cash, year, jan, feb) = await SeedAsync();
        var service = new BudgetService(ctx);

        // Budget: 10,000 revenue and 4,000 expense across Jan + Feb combined.
        await service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = revenue.Id, FiscalPeriodId = jan.Id, Amount = 6000m });
        await service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = revenue.Id, FiscalPeriodId = feb.Id, Amount = 4000m });
        await service.SetBudgetLineAsync(new SetBudgetLineDto { AccountId = expense.Id, FiscalPeriodId = jan.Id, Amount = 4000m });

        // Actual: a posted sale of 7,000 in January (Dr Cash / Cr Revenue) and a posted expense of 5,000 (Dr Expense / Cr Cash).
        ctx.JournalEntries.Add(new JournalEntry
        {
            EntryNumber = "JV-000001",
            EntryDate = new DateTime(2026, 1, 15),
            FiscalPeriodId = jan.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = cash.Id, Debit = 7000m, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 7000m }
            }
        });
        ctx.JournalEntries.Add(new JournalEntry
        {
            EntryNumber = "JV-000002",
            EntryDate = new DateTime(2026, 1, 20),
            FiscalPeriodId = jan.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = expense.Id, Debit = 5000m, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = cash.Id, Debit = 0, Credit = 5000m }
            }
        });
        await ctx.SaveChangesAsync();

        var report = await service.GetBudgetVsActualAsync(year.Id);

        var revenueLine = Assert.Single(report.RevenueLines);
        Assert.Equal(10000m, revenueLine.BudgetedAmount);
        Assert.Equal(7000m, revenueLine.ActualAmount);
        Assert.Equal(-3000m, revenueLine.VarianceAmount);

        var expenseLine = Assert.Single(report.ExpenseLines);
        Assert.Equal(4000m, expenseLine.BudgetedAmount);
        Assert.Equal(5000m, expenseLine.ActualAmount);
        Assert.Equal(1000m, expenseLine.VarianceAmount);

        Assert.Equal(10000m, report.TotalBudgetedRevenue);
        Assert.Equal(7000m, report.TotalActualRevenue);
        Assert.Equal(4000m, report.TotalBudgetedExpense);
        Assert.Equal(5000m, report.TotalActualExpense);
        Assert.Equal(6000m, report.BudgetedNetIncome);
        Assert.Equal(2000m, report.ActualNetIncome);
    }
}
