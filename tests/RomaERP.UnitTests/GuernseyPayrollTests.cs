using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class GuernseyPayrollTests
{
    [Fact]
    public void Calculator_TaxesPayAboveTheAllowanceAndChargesSocialSecurityUpToTheCeiling()
    {
        var r = GuernseyPayrollCalculator.Calculate(3000m, 20m, 15200m, 7.5m, 7.1m, 15717m);

        Assert.Equal(346.67m, r.IncomeTax);        // (36,000 - 15,200) x 20% / 12
        Assert.Equal(225.00m, r.EmployeeSocial);   // 7.5% of 3,000
        Assert.Equal(213.00m, r.EmployerSocial);   // 7.1% of 3,000
    }

    [Fact]
    public void Calculator_CapsSocialSecurityAtTheMonthlyCeiling_AndIgnoresTheCeilingWhenZero()
    {
        var capped = GuernseyPayrollCalculator.Calculate(20000m, 20m, 15200m, 7.5m, 7.1m, 15717m);
        var uncapped = GuernseyPayrollCalculator.Calculate(20000m, 20m, 15200m, 7.5m, 7.1m, 0m);

        Assert.Equal(1178.78m, capped.EmployeeSocial);   // 7.5% of 15,717
        Assert.Equal(1500.00m, uncapped.EmployeeSocial); // 7.5% of 20,000
    }

    [Fact]
    public void Calculator_LowPay_PaysNoTax()
        => Assert.Equal(0m, GuernseyPayrollCalculator.Calculate(1000m, 20m, 15200m, 7.5m, 7.1m, 15717m).IncomeTax);

    [Fact]
    public async Task GuernseyCompany_RunsPayrollWithTheCompanysOwnRates_AndKeepsTheJournalBalanced()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var ctx = new ApplicationDbContext(options);
        var employee = new Employee { EmployeeCode = "E1", FullNameAr = "م", FullNameEn = "Mia", HireDate = new DateTime(2025, 1, 1), BasicSalary = 3000 };
        var period = new FiscalPeriod { Name = "September 2026", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        ctx.Employees.Add(employee);
        ctx.FiscalPeriods.Add(period);
        ctx.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "ش", CompanyNameEn = "Co", PayrollDaysPerMonth = 30, Country = Country.Guernsey,
            GgIncomeTaxRatePercent = 20m, GgPersonalAllowanceAnnual = 15200m, GgEmployeeSocialRatePercent = 7m, GgEmployerSocialRatePercent = 6.5m, GgSocialMonthlyUpperLimit = 0m
        });
        ctx.Accounts.AddRange(
            new Account { Code = AccountingConstants.SalariesExpenseAccountCode, NameAr = "a", NameEn = "Salaries", AccountType = AccountType.Expense, Nature = AccountNature.Debit },
            new Account { Code = AccountingConstants.AccruedSalariesPayableAccountCode, NameAr = "b", NameEn = "Accrued", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
            new Account { Code = AccountingConstants.TaxesPayableAccountCode, NameAr = "c", NameEn = "Taxes", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
            new Account { Code = AccountingConstants.SocialInsurancePayableAccountCode, NameAr = "d", NameEn = "Insurance payable", AccountType = AccountType.Liability, Nature = AccountNature.Credit },
            new Account { Code = AccountingConstants.EmployerSocialInsuranceExpenseAccountCode, NameAr = "e", NameEn = "Employer insurance", AccountType = AccountType.Expense, Nature = AccountNature.Debit });
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        var run = await service.CreateAndCalculateAsync(new CreatePayrollRunDto { FiscalPeriodId = period.Id, RunDate = new DateTime(2026, 9, 30) });

        var line = Assert.Single(run.Lines);
        Assert.Equal(346.67m, line.GgIncomeTax);
        Assert.Equal(210.00m, line.GgEmployeeSocial);   // the company's own 7%
        Assert.Equal(195.00m, line.GgEmployerSocial);   // the company's own 6.5%
        Assert.Equal(3000m - 346.67m - 210.00m, line.NetSalary);

        await service.ApproveAsync(run.Id);
        await service.PostAsync(run.Id);
        var posted = await ctx.PayrollRuns.SingleAsync(r => r.Id == run.Id);
        Assert.True((await ctx.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == posted.JournalEntryId)).IsBalanced);
    }

    [Fact]
    public async Task GuernseySettings_AreSaved_ButOnlyForAGuernseyCompany()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var ctx = new ApplicationDbContext(options);
        ctx.CompanySettings.Add(new CompanySettings { CompanyNameAr = "ش", CompanyNameEn = "Co", Country = Country.Guernsey, PayrollDaysPerMonth = 30 });
        await ctx.SaveChangesAsync();
        var service = new PayrollService(ctx);

        var dto = await service.GetSettingsAsync();
        dto.GgEmployeeSocialRatePercent = 7m;
        dto.GgIncomeTaxRatePercent = 22m;
        var saved = await service.UpdateSettingsAsync(dto);

        Assert.True(saved.IsGuernseyPayroll);
        Assert.Equal(7m, saved.GgEmployeeSocialRatePercent);
        Assert.Equal(22m, saved.GgIncomeTaxRatePercent);
    }
}
