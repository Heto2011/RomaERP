using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Tenancy;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.Persistence.Seed;

/// <summary>Baseline data every tenant database needs — chart of accounts, fiscal year, cost center,
/// a default department/position, inventory basics, and company settings. Used by both the dev-seed
/// path (DbInitializer, for the demo tenant) and TenantProvisioningService (for real client tenants).</summary>
public static class TenantBaselineSeeder
{
    public static async Task SeedChartOfAccountsAsync(ApplicationDbContext context)
    {
        if (await context.Accounts.AnyAsync())
            return;

        var accounts = ChartOfAccountsFactory.BuildAccounts();
        await context.Accounts.AddRangeAsync(accounts);
        await context.SaveChangesAsync();
    }

    public static async Task SeedFiscalYearAsync(ApplicationDbContext context)
    {
        if (await context.FiscalYears.AnyAsync())
            return;

        var year = DateTime.UtcNow.Year;
        var fiscalYear = new FiscalYear
        {
            Name = $"السنة المالية {year}",
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 12, 31),
            IsClosed = false
        };

        var periods = new List<FiscalPeriod>();
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateTime(year, month, 1);
            periods.Add(new FiscalPeriod
            {
                FiscalYearId = fiscalYear.Id,
                Name = start.ToString("MMMM yyyy"),
                PeriodNumber = month,
                StartDate = start,
                EndDate = start.AddMonths(1).AddDays(-1),
                IsClosed = false
            });
        }

        fiscalYear.Periods = periods;
        context.FiscalYears.Add(fiscalYear);
        await context.SaveChangesAsync();
    }

    public static async Task SeedCostCenterAsync(ApplicationDbContext context)
    {
        if (await context.CostCenters.AnyAsync())
            return;

        context.CostCenters.Add(new CostCenter
        {
            Code = "CC-000",
            NameAr = "مركز التكلفة العام",
            NameEn = "General Cost Center",
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    public static async Task SeedDepartmentAsync(ApplicationDbContext context)
    {
        if (await context.Departments.AnyAsync())
            return;

        var management = new Department
        {
            Code = "DEP-000",
            NameAr = "الإدارة العامة",
            NameEn = "General Management",
            IsActive = true
        };

        context.Departments.Add(management);
        await context.SaveChangesAsync();

        context.Positions.Add(new Position
        {
            Code = "POS-000",
            TitleAr = "مدير عام",
            TitleEn = "General Manager",
            DepartmentId = management.Id,
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    public static async Task SeedInventoryAsync(ApplicationDbContext context)
    {
        if (await context.Warehouses.AnyAsync())
            return;

        context.Warehouses.Add(new Warehouse
        {
            Code = "WH-000",
            NameAr = "المخزن الرئيسي",
            NameEn = "Main Warehouse",
            IsActive = true
        });

        context.ItemCategories.Add(new ItemCategory
        {
            Code = "CAT-000",
            NameAr = "تصنيف عام",
            NameEn = "General",
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    public static async Task SeedCompanySettingsAsync(ApplicationDbContext context, Country country, string companyNameAr, string companyNameEn, string? taxRegistrationNumber)
    {
        if (await context.CompanySettings.AnyAsync())
            return;

        var taxDefaults = CountryTaxDefaults.Get(country);
        context.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = companyNameAr,
            CompanyNameEn = companyNameEn,
            Country = country,
            TaxRegistrationNumber = taxRegistrationNumber,
            VatRate = taxDefaults.VatRate,
            DefaultCurrency = taxDefaults.Currency
        });

        await context.SaveChangesAsync();
    }
}
