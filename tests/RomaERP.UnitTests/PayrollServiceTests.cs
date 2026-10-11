using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
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
    public async Task CreateAndCalculateAsync_ComputesGosiPerCategory_SaudiFullSplit_NonSaudiEmployerHazardsOnly()
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
            GosiEnabled = true, GosiEmployeeRatePercent = 9.75m, GosiEmployerAnnuitiesRatePercent = 9.75m, GosiEmployerHazardsRatePercent = 2m,
            GosiNonSaudiEmployerHazardsRatePercent = 1.5m
        });
        await ctx.SaveChangesAsync();

        var service = new PayrollService(ctx);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });

        var saudiLine = run.Lines.Single(l => l.EmployeeId == saudiEmployee.Id);
        Assert.Equal(975m, saudiLine.GosiEmployeeDeductionAmount); // 10000 * 9.75%
        Assert.Equal(1175m, saudiLine.GosiEmployerContributionAmount); // 10000 * (9.75% + 2%)
        Assert.Equal(975m, saudiLine.TotalDeductions);
        Assert.Equal(9025m, saudiLine.NetSalary);

        // Non-Saudi: no employee withholding and no employer Annuities — just the employer's own
        // (usually lower) Occupational Hazards rate, at its own configured percentage.
        var nonSaudiLine = run.Lines.Single(l => l.EmployeeId == nonSaudiEmployee.Id);
        Assert.Equal(0m, nonSaudiLine.GosiEmployeeDeductionAmount);
        Assert.Equal(150m, nonSaudiLine.GosiEmployerContributionAmount); // 10000 * 1.5%
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

    private static async Task<(ApplicationDbContext Ctx, Employee Employee, FiscalPeriod April, FiscalPeriod May, PayrollService Service)> SeedUkAsync(
        Action<Employee>? configure = null, bool ukCompany = true, bool withAccounts = false)
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 3000);
        configure?.Invoke(employee);
        var april = new FiscalPeriod { Name = "April 2026", PeriodNumber = 4, StartDate = new DateTime(2026, 4, 1), EndDate = new DateTime(2026, 4, 30) };
        var may = new FiscalPeriod { Name = "May 2026", PeriodNumber = 5, StartDate = new DateTime(2026, 5, 1), EndDate = new DateTime(2026, 5, 31) };
        ctx.Employees.Add(employee);
        ctx.FiscalPeriods.AddRange(april, may);
        ctx.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30,
            Country = ukCompany ? Country.UnitedKingdom : Country.SaudiArabia
        });
        if (withAccounts)
        {
            ctx.Accounts.AddRange(
                new Account { Code = AccountingConstants.SalariesExpenseAccountCode, NameAr = "a", NameEn = "Salaries", AccountType = AccountType.Expense, Nature = AccountNature.Debit },
                new Account { Code = AccountingConstants.AccruedSalariesPayableAccountCode, NameAr = "b", NameEn = "Accrued", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
                new Account { Code = AccountingConstants.HmrcPayableAccountCode, NameAr = "c", NameEn = "HMRC", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
                new Account { Code = AccountingConstants.PensionPayableAccountCode, NameAr = "d", NameEn = "Pension", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
                new Account { Code = AccountingConstants.EmployerNiExpenseAccountCode, NameAr = "e", NameEn = "Employer NI", AccountType = AccountType.Expense, Nature = AccountNature.Debit },
                new Account { Code = AccountingConstants.EmployerPensionExpenseAccountCode, NameAr = "f", NameEn = "Employer pension", AccountType = AccountType.Expense, Nature = AccountNature.Debit });
        }
        await ctx.SaveChangesAsync();
        return (ctx, employee, april, may, new PayrollService(ctx));
    }

    [Fact]
    public async Task UkCompany_ComputesPayeNationalInsuranceAndTheNetPay()
    {
        var (_, _, april, _, service) = await SeedUkAsync();

        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });

        var line = Assert.Single(run.Lines);
        Assert.Equal(390.20m, line.UkIncomeTax);
        Assert.Equal(156.16m, line.UkEmployeeNi);
        Assert.Equal(387.45m, line.UkEmployerNi);
        Assert.Equal(390.20m + 156.16m, line.TotalDeductions);
        Assert.Equal(3000m - 390.20m - 156.16m, line.NetSalary);
    }

    [Fact]
    public async Task UkCompany_TaxIsCumulativeAcrossTheRunsOfTheSameTaxYear()
    {
        var (_, _, april, may, service) = await SeedUkAsync();
        await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });

        var second = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = may.Id, RunDate = new DateTime(2026, 5, 28) });

        Assert.Equal(390.40m, Assert.Single(second.Lines).UkIncomeTax);
    }

    [Fact]
    public async Task NonUkCompany_GetsNoUkDeductions()
    {
        var (_, _, april, _, service) = await SeedUkAsync(ukCompany: false);

        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });

        var line = Assert.Single(run.Lines);
        Assert.Equal(0m, line.UkIncomeTax);
        Assert.Equal(3000m, line.NetSalary);
    }

    [Fact]
    public async Task UkCompany_RejectsAScottishTaxCodeWithTheEmployeeName()
    {
        var (_, _, april, _, service) = await SeedUkAsync(e => e.UkTaxCode = "S1257L");

        var ex = await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) }));
        Assert.Contains("Ehab Salah", ex.Message);
    }

    [Fact]
    public async Task UkPosting_StaysBalanced_WithPensionStudentLoanAndEmployerCosts()
    {
        var (ctx, _, april, _, service) = await SeedUkAsync(e =>
        {
            e.UkPensionEnrolled = true;
            e.UkStudentLoanPlan = UkStudentLoanPlan.Plan2;
        }, withAccounts: true);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });
        await service.ApproveAsync(run.Id);

        await service.PostAsync(run.Id);

        var posted = await ctx.PayrollRuns.SingleAsync(r => r.Id == run.Id);
        var entry = await ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.True(entry.IsBalanced);
        var hmrc = await ctx.Accounts.SingleAsync(a => a.Code == AccountingConstants.HmrcPayableAccountCode);
        Assert.Contains(entry.Lines, l => l.AccountId == hmrc.Id && l.Credit > 0);
    }

    [Fact]
    public async Task UkSummaryCsv_ListsEachEmployeeWithTheirFiguresAndTheAmountOwedToHmrc()
    {
        var (_, _, april, _, service) = await SeedUkAsync(e => { e.UkNationalInsuranceNumber = "QQ123456C"; e.UkTaxCode = "1257L"; });
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });

        var csv = await service.BuildUkSummaryCsvAsync(run.Id);

        Assert.Contains("Ehab Salah,EMP-001,QQ123456C,1257L,A,3000.00,3000.00,390.20,156.16,387.45", csv);
        Assert.Contains("Owed to HMRC for this run (income tax + employee NI + employer NI + student loans),933.81", csv); // 390.20 + 156.16 + 387.45
        Assert.Contains("does not submit anything to HMRC", csv);
    }

    private class CapturingRenderer : IHtmlToPdfRenderer
    {
        public string? Html;
        public Task<byte[]> RenderAsync(string html, CancellationToken ct = default)
        {
            Html = html;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }

    [Fact]
    public async Task Payslip_ShowsEarningsDeductionsNetPayAndEmployerCosts_AndEncodesNames()
    {
        var (ctx, employee, april, _, _) = await SeedUkAsync(e => { e.FullNameEn = "<b>Eve</b>"; e.UkNationalInsuranceNumber = "QQ123456C"; });
        var renderer = new CapturingRenderer();
        var service = new PayrollService(ctx, renderer);
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });

        var pdf = await service.GetPayslipPdfAsync(run.Id, employee.Id, arabic: false, publishedOnly: false);

        Assert.Equal(3, pdf.Length);
        Assert.Contains("Payslip", renderer.Html);
        Assert.Contains("2,453.64", renderer.Html);        // net pay
        Assert.Contains("390.20", renderer.Html);          // income tax
        Assert.Contains("387.45", renderer.Html);          // employer National Insurance
        Assert.Contains("QQ123456C", renderer.Html);
        Assert.DoesNotContain("<b>Eve</b>", renderer.Html); // names are HTML-encoded
    }

    [Fact]
    public async Task Payslip_IsNotAvailableToTheEmployeeWhileTheRunIsStillADraft()
    {
        var (ctx, employee, april, _, _) = await SeedUkAsync();
        var service = new PayrollService(ctx, new CapturingRenderer());
        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = april.Id, RunDate = new DateTime(2026, 4, 28) });

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPayslipPdfAsync(run.Id, employee.Id, arabic: true, publishedOnly: true));
    }

    [Fact]
    public async Task Gosi_IsCappedAtTheMonthlyCeiling()
    {
        var ctx = CreateContext();
        var employee = CreateEmployee(basicSalary: 60000);
        employee.IsSaudiNational = true;
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "شركة", CompanyNameEn = "Co", PayrollDaysPerMonth = 30,
            GosiEnabled = true, GosiEmployeeRatePercent = 10m, GosiEmployerAnnuitiesRatePercent = 10m, GosiEmployerHazardsRatePercent = 2m
        });
        await ctx.SaveChangesAsync();

        var run = await new PayrollService(ctx).CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });

        var line = Assert.Single(run.Lines);
        Assert.Equal(4500m, line.GosiEmployeeDeductionAmount);      // 10% of the 45,000 ceiling, not of 60,000
        Assert.Equal(5400m, line.GosiEmployerContributionAmount);   // 12% of 45,000
    }
}
