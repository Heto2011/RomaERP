using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await SeedRolesAndAdminAsync(userManager, roleManager);
        await SeedChartOfAccountsAsync(context);
        await SeedFiscalYearAsync(context);
        await SeedCostCenterAsync(context);
        await SeedDepartmentAsync(context);
    }

    private static async Task SeedRolesAndAdminAsync(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        string[] roles = { "Admin", "Accountant", "HR", "Employee" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        const string adminEmail = "admin@romaerp.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    private static async Task SeedChartOfAccountsAsync(ApplicationDbContext context)
    {
        if (await context.Accounts.AnyAsync())
            return;

        Account New(string code, string ar, string en, AccountType type, AccountNature nature, Account? parent, bool control, int level)
            => new()
            {
                Code = code,
                NameAr = ar,
                NameEn = en,
                AccountType = type,
                Nature = nature,
                ParentAccountId = parent?.Id,
                IsControlAccount = control,
                Level = level,
                IsActive = true
            };

        var accounts = new List<Account>();

        // ===== 1000 الأصول =====
        var assets = New("1000", "الأصول", "Assets", AccountType.Asset, AccountNature.Debit, null, true, 1);
        var currentAssets = New("1100", "الأصول المتداولة", "Current Assets", AccountType.Asset, AccountNature.Debit, assets, true, 2);
        var cashAndEquivalents = New("1110", "النقدية وما في حكمها", "Cash and Cash Equivalents", AccountType.Asset, AccountNature.Debit, currentAssets, true, 3);
        var cashOnHand = New("1111", "الصندوق", "Cash on Hand", AccountType.Asset, AccountNature.Debit, cashAndEquivalents, false, 4);
        var bank = New("1112", "البنك", "Bank", AccountType.Asset, AccountNature.Debit, cashAndEquivalents, false, 4);
        var accountsReceivable = New("1120", "العملاء", "Accounts Receivable", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);
        var notesReceivable = New("1130", "أوراق قبض", "Notes Receivable", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);
        var prepaidExpenses = New("1140", "مصروفات مقدمة", "Prepaid Expenses", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);
        var accruedRevenue = New("1150", "إيرادات مستحقة", "Accrued Revenue", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);
        var inventory = New("1160", "المخزون", "Inventory", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);

        var fixedAssets = New("1200", "الأصول الثابتة", "Fixed Assets", AccountType.Asset, AccountNature.Debit, assets, true, 2);
        var landAndBuildings = New("1210", "أراضي ومباني", "Land & Buildings", AccountType.Asset, AccountNature.Debit, fixedAssets, false, 3);
        var machinery = New("1220", "آلات ومعدات", "Machinery & Equipment", AccountType.Asset, AccountNature.Debit, fixedAssets, false, 3);
        var furniture = New("1230", "أثاث وتجهيزات", "Furniture & Fixtures", AccountType.Asset, AccountNature.Debit, fixedAssets, false, 3);
        var accumulatedDepreciation = New("1240", "مجمع الإهلاك", "Accumulated Depreciation", AccountType.Asset, AccountNature.Credit, fixedAssets, false, 3);

        accounts.AddRange(new[]
        {
            assets, currentAssets, cashAndEquivalents, cashOnHand, bank, accountsReceivable, notesReceivable,
            prepaidExpenses, accruedRevenue, inventory,
            fixedAssets, landAndBuildings, machinery, furniture, accumulatedDepreciation
        });

        // ===== 2000 الخصوم =====
        var liabilities = New("2000", "الخصوم", "Liabilities", AccountType.Liability, AccountNature.Credit, null, true, 1);
        var currentLiabilities = New("2100", "الخصوم المتداولة", "Current Liabilities", AccountType.Liability, AccountNature.Credit, liabilities, true, 2);
        var accruedSalaries = New("2110", "مرتبات مستحقة", "Accrued Salaries Payable", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);
        var accountsPayable = New("2120", "الموردون", "Accounts Payable", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);
        var notesPayable = New("2130", "أوراق دفع", "Notes Payable", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);
        var unearnedRevenue = New("2140", "إيرادات مقدمة", "Unearned Revenue", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);
        var otherAccruedExpenses = New("2150", "مصروفات مستحقة أخرى", "Other Accrued Expenses", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);
        var taxesPayable = New("2160", "ضرائب مستحقة", "Taxes Payable", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);

        var longTermLiabilities = New("2200", "الخصوم طويلة الأجل", "Long-term Liabilities", AccountType.Liability, AccountNature.Credit, liabilities, true, 2);
        var longTermLoans = New("2210", "قروض طويلة الأجل", "Long-term Loans", AccountType.Liability, AccountNature.Credit, longTermLiabilities, false, 3);

        accounts.AddRange(new[]
        {
            liabilities, currentLiabilities, accruedSalaries, accountsPayable, notesPayable,
            unearnedRevenue, otherAccruedExpenses, taxesPayable,
            longTermLiabilities, longTermLoans
        });

        // ===== 3000 حقوق الملكية =====
        var equity = New("3000", "حقوق الملكية", "Equity", AccountType.Equity, AccountNature.Credit, null, true, 1);
        var capital = New("3100", "رأس المال", "Capital", AccountType.Equity, AccountNature.Credit, equity, false, 2);
        var retainedEarnings = New("3200", "أرباح مرحلة", "Retained Earnings", AccountType.Equity, AccountNature.Credit, equity, false, 2);
        var currentYearEarnings = New("3300", "أرباح العام الحالي", "Current Year Earnings", AccountType.Equity, AccountNature.Credit, equity, false, 2);

        accounts.AddRange(new[] { equity, capital, retainedEarnings, currentYearEarnings });

        // ===== 4000 الإيرادات =====
        var revenue = New("4000", "الإيرادات", "Revenue", AccountType.Revenue, AccountNature.Credit, null, true, 1);
        var salesRevenue = New("4100", "إيرادات المبيعات", "Sales Revenue", AccountType.Revenue, AccountNature.Credit, revenue, false, 2);
        var otherRevenue = New("4200", "إيرادات أخرى", "Other Revenue", AccountType.Revenue, AccountNature.Credit, revenue, false, 2);

        accounts.AddRange(new[] { revenue, salesRevenue, otherRevenue });

        // ===== 5000 المصروفات =====
        var expenses = New("5000", "المصروفات", "Expenses", AccountType.Expense, AccountNature.Debit, null, true, 1);
        var salariesExpense = New("5100", "مصروف المرتبات والأجور", "Salaries and Wages Expense", AccountType.Expense, AccountNature.Debit, expenses, false, 2);
        var rentExpense = New("5200", "إيجارات", "Rent Expense", AccountType.Expense, AccountNature.Debit, expenses, false, 2);
        var adminExpenses = New("5300", "مصروفات إدارية وعمومية", "General & Admin Expenses", AccountType.Expense, AccountNature.Debit, expenses, false, 2);
        var depreciationExpense = New("5400", "مصروف الإهلاك", "Depreciation Expense", AccountType.Expense, AccountNature.Debit, expenses, false, 2);
        var costOfGoodsSold = New("5500", "تكلفة البضاعة المباعة", "Cost of Goods Sold", AccountType.Expense, AccountNature.Debit, expenses, false, 2);

        accounts.AddRange(new[]
        {
            expenses, salariesExpense, rentExpense, adminExpenses, depreciationExpense, costOfGoodsSold
        });

        await context.Accounts.AddRangeAsync(accounts);
        await context.SaveChangesAsync();
    }

    private static async Task SeedFiscalYearAsync(ApplicationDbContext context)
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

    private static async Task SeedCostCenterAsync(ApplicationDbContext context)
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

    private static async Task SeedDepartmentAsync(ApplicationDbContext context)
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
}
