using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PayrollServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Employee CreateEmployee(decimal basicSalary = 10000)
    {
        return new Employee
        {
            EmployeeCode = "EMP-001",
            FullNameAr = "إيهاب صلاح",
            FullNameEn = "Ehab Salah",
            HireDate = new DateTime(2025, 1, 1),
            BasicSalary = basicSalary
        };
    }

    private static async Task<(ApplicationDbContext ctx, PayrollRun run, Employee employee)> SeedDraftRunAsync()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee();
        ctx.Employees.Add(employee);

        var run = new PayrollRun
        {
            RunDate = new DateTime(2026, 8, 29),
            Description = "دورة أغسطس",
            Status = PayrollRunStatus.Draft,
            Lines =
            {
                new PayrollRunLine { EmployeeId = employee.Id, BasicSalary = 10000, TotalAllowances = 0, TotalDeductions = 0, NetSalary = 10000 }
            }
        };
        ctx.PayrollRuns.Add(run);
        await ctx.SaveChangesAsync();

        return (ctx, run, employee);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesDraftRun_AndHidesItFromSubsequentQueries()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        var service = new PayrollService(ctx);

        await service.DeleteAsync(run.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(run.Id));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenRunIsNotDraft()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        run.Status = PayrollRunStatus.Approved;
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.DeleteAsync(run.Id));
    }

    [Fact]
    public async Task RevertToDraftAsync_MovesApprovedRunBackToDraft()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        run.Status = PayrollRunStatus.Approved;
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        var result = await service.RevertToDraftAsync(run.Id);

        Assert.Equal(PayrollRunStatus.Draft, result.Status);
    }

    [Fact]
    public async Task RevertToDraftAsync_ThrowsWhenRunIsAlreadyDraft()
    {
        var (ctx, run, _) = await SeedDraftRunAsync();
        var service = new PayrollService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RevertToDraftAsync(run.Id));
    }

    [Fact]
    public async Task UpdateLineAsync_RecalculatesNetSalary_WhenRunIsDraft()
    {
        var (ctx, run, employee) = await SeedDraftRunAsync();
        var service = new PayrollService(ctx);

        var result = await service.UpdateLineAsync(run.Id, employee.Id, new UpdatePayrollLineDto { TotalAllowances = 1500, TotalDeductions = 200 });

        var line = Assert.Single(result.Lines);
        Assert.Equal(1500, line.TotalAllowances);
        Assert.Equal(200, line.TotalDeductions);
        Assert.Equal(11300, line.NetSalary); // 10000 + 1500 - 200
    }

    [Fact]
    public async Task UpdateLineAsync_ThrowsWhenRunIsNotDraft()
    {
        var (ctx, run, employee) = await SeedDraftRunAsync();
        run.Status = PayrollRunStatus.Approved;
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        await Assert.ThrowsAsync<ValidationAppException>(
            () => service.UpdateLineAsync(run.Id, employee.Id, new UpdatePayrollLineDto { TotalAllowances = 100, TotalDeductions = 0 }));
    }

    [Fact]
    public async Task CreateAndCalculateAsync_DeductsApprovedUnpaidLeaveOverlappingThePeriod()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 3000);
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30 });
        ctx.EmployeeRequests.Add(new EmployeeRequest
        {
            EmployeeId = employee.Id,
            Type = EmployeeRequestType.Leave,
            Status = EmployeeRequestStatus.Approved,
            DateFrom = new DateTime(2026, 9, 10),
            DateTo = new DateTime(2026, 9, 11)
        });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });

        var line = Assert.Single(run.Lines);
        Assert.Equal(2, line.UnpaidLeaveDays);
        Assert.Equal(200m, line.UnpaidLeaveDeductionAmount); // 3000/30 * 2 days
        Assert.Equal(200m, line.TotalDeductions);
        Assert.Equal(2800m, line.NetSalary);
    }

    [Fact]
    public async Task PostAsync_KeepsTheJournalEntryBalanced_WhenALineHasAnUnpaidLeaveDeduction()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 3000);
        var salariesExpense = new Account { Code = AccountingConstants.SalariesExpenseAccountCode, NameAr = "مصروف مرتبات", NameEn = "Salaries Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var accruedSalaries = new Account { Code = AccountingConstants.AccruedSalariesPayableAccountCode, NameAr = "مرتبات مستحقة", NameEn = "Accrued Salaries", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.Accounts.AddRange(salariesExpense, accruedSalaries);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30 });
        ctx.EmployeeRequests.Add(new EmployeeRequest
        {
            EmployeeId = employee.Id,
            Type = EmployeeRequestType.Leave,
            Status = EmployeeRequestStatus.Approved,
            DateFrom = new DateTime(2026, 9, 10)
        });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });
        await service.ApproveAsync(run.Id);
        await service.PostAsync(run.Id);

        var postedRun = await ctx.PayrollRuns.SingleAsync(r => r.Id == run.Id);
        var journalEntry = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == postedRun.JournalEntryId);

        Assert.True(journalEntry.IsBalanced);
        Assert.Equal(journalEntry.TotalDebit, journalEntry.TotalCredit);
    }

    [Fact]
    public async Task CreateAndCalculateAsync_ComputesGosi_OnlyForSaudiNationalsWhenEnabled()
    {
        var ctx = CreateContext();
        var saudiEmployee = CreateEmployee(basicSalary: 10000);
        saudiEmployee.IsSaudiNational = true;
        var nonSaudiEmployee = new Employee
        {
            EmployeeCode = "EMP-002", FullNameAr = "موظف أجنبي", FullNameEn = "Foreign Employee",
            HireDate = new DateTime(2025, 1, 1), BasicSalary = 10000, IsSaudiNational = false
        };
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.AddRange(saudiEmployee, nonSaudiEmployee);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30,
            GosiEnabled = true, GosiEmployeeRatePercent = 9.75m, GosiEmployerAnnuitiesRatePercent = 9.75m, GosiEmployerHazardsRatePercent = 2m
        });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });

        var saudiLine = run.Lines.Single(l => l.EmployeeId == saudiEmployee.Id);
        Assert.Equal(975m, saudiLine.GosiEmployeeDeductionAmount); // 10000 * 9.75%
        Assert.Equal(1175m, saudiLine.GosiEmployerContributionAmount); // 10000 * (9.75% + 2%)
        Assert.Equal(975m, saudiLine.TotalDeductions);
        Assert.Equal(9025m, saudiLine.NetSalary);

        var nonSaudiLine = run.Lines.Single(l => l.EmployeeId == nonSaudiEmployee.Id);
        Assert.Equal(0m, nonSaudiLine.GosiEmployeeDeductionAmount);
        Assert.Equal(0m, nonSaudiLine.GosiEmployerContributionAmount);
        Assert.Equal(10000m, nonSaudiLine.NetSalary);
    }

    [Fact]
    public async Task CreateAndCalculateAsync_NoGosi_WhenDisabledEvenForSaudiNational()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 10000);
        employee.IsSaudiNational = true;
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30, GosiEnabled = false });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });

        var line = Assert.Single(run.Lines);
        Assert.Equal(0m, line.GosiEmployeeDeductionAmount);
        Assert.Equal(0m, line.GosiEmployerContributionAmount);
        Assert.Equal(10000m, line.NetSalary);
    }

    [Fact]
    public async Task PostAsync_KeepsTheJournalEntryBalanced_AndPostsGosiPayable_WhenALineHasGosi()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 10000);
        employee.IsSaudiNational = true;
        var salariesExpense = new Account { Code = AccountingConstants.SalariesExpenseAccountCode, NameAr = "مصروف مرتبات", NameEn = "Salaries Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var accruedSalaries = new Account { Code = AccountingConstants.AccruedSalariesPayableAccountCode, NameAr = "مرتبات مستحقة", NameEn = "Accrued Salaries", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var gosiEmployerExpense = new Account { Code = AccountingConstants.GosiEmployerExpenseAccountCode, NameAr = "مصروف تأمينات", NameEn = "GOSI Employer Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var gosiPayable = new Account { Code = AccountingConstants.GosiPayableAccountCode, NameAr = "تأمينات مستحقة", NameEn = "GOSI Payable", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.Accounts.AddRange(salariesExpense, accruedSalaries, gosiEmployerExpense, gosiPayable);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30,
            GosiEnabled = true, GosiEmployeeRatePercent = 9.75m, GosiEmployerAnnuitiesRatePercent = 9.75m, GosiEmployerHazardsRatePercent = 2m
        });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });
        await service.ApproveAsync(run.Id);
        await service.PostAsync(run.Id);

        var postedRun = await ctx.PayrollRuns.SingleAsync(r => r.Id == run.Id);
        var journalEntry = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == postedRun.JournalEntryId);

        Assert.True(journalEntry.IsBalanced);
        Assert.Equal(journalEntry.TotalDebit, journalEntry.TotalCredit);

        Assert.Contains(journalEntry.Lines, l => l.AccountId == gosiEmployerExpense.Id && l.Debit == 1175m);
        Assert.Contains(journalEntry.Lines, l => l.AccountId == gosiPayable.Id && l.Credit == 2150m); // 975 employee + 1175 employer
        Assert.Contains(journalEntry.Lines, l => l.AccountId == salariesExpense.Id && l.Debit == 10000m); // unaffected by GOSI
    }

    [Fact]
    public async Task PostAsync_ThrowsAClearError_WhenGosiAccountsAreMissing()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 10000);
        employee.IsSaudiNational = true;
        var salariesExpense = new Account { Code = AccountingConstants.SalariesExpenseAccountCode, NameAr = "مصروف مرتبات", NameEn = "Salaries Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };
        var accruedSalaries = new Account { Code = AccountingConstants.AccruedSalariesPayableAccountCode, NameAr = "مرتبات مستحقة", NameEn = "Accrued Salaries", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.Accounts.AddRange(salariesExpense, accruedSalaries); // GOSI accounts deliberately not seeded
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30,
            GosiEnabled = true, GosiEmployeeRatePercent = 9.75m, GosiEmployerAnnuitiesRatePercent = 9.75m, GosiEmployerHazardsRatePercent = 2m
        });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });
        await service.ApproveAsync(run.Id);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.PostAsync(run.Id));
    }
}
