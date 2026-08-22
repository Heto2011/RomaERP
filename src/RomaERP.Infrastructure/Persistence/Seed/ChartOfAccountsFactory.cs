using RomaERP.Domain.Accounting;

namespace RomaERP.Infrastructure.Persistence.Seed;

/// <summary>Builds the standard chart of accounts given to every new tenant. Shared by the dev-seed
/// path (DbInitializer) and tenant provisioning so both stay in sync automatically.</summary>
public static class ChartOfAccountsFactory
{
    public static List<Account> BuildAccounts()
    {
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
        var employeeCustodies = New("1170", "عهد الموظفين", "Employee Custodies", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);
        var inputVat = New("1180", "ضريبة القيمة المضافة (مدخلات)", "Input VAT", AccountType.Asset, AccountNature.Debit, currentAssets, false, 3);

        var fixedAssets = New("1200", "الأصول الثابتة", "Fixed Assets", AccountType.Asset, AccountNature.Debit, assets, true, 2);
        var landAndBuildings = New("1210", "أراضي ومباني", "Land & Buildings", AccountType.Asset, AccountNature.Debit, fixedAssets, false, 3);
        var machinery = New("1220", "آلات ومعدات", "Machinery & Equipment", AccountType.Asset, AccountNature.Debit, fixedAssets, false, 3);
        var furniture = New("1230", "أثاث وتجهيزات", "Furniture & Fixtures", AccountType.Asset, AccountNature.Debit, fixedAssets, false, 3);
        var accumulatedDepreciation = New("1240", "مجمع الإهلاك", "Accumulated Depreciation", AccountType.Asset, AccountNature.Credit, fixedAssets, false, 3);

        accounts.AddRange(new[]
        {
            assets, currentAssets, cashAndEquivalents, cashOnHand, bank, accountsReceivable, notesReceivable,
            prepaidExpenses, accruedRevenue, inventory, employeeCustodies, inputVat,
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
        var outputVat = New("2161", "ضريبة القيمة المضافة (مخرجات)", "Output VAT", AccountType.Liability, AccountNature.Credit, currentLiabilities, false, 3);

        var longTermLiabilities = New("2200", "الخصوم طويلة الأجل", "Long-term Liabilities", AccountType.Liability, AccountNature.Credit, liabilities, true, 2);
        var longTermLoans = New("2210", "قروض طويلة الأجل", "Long-term Loans", AccountType.Liability, AccountNature.Credit, longTermLiabilities, false, 3);

        accounts.AddRange(new[]
        {
            liabilities, currentLiabilities, accruedSalaries, accountsPayable, notesPayable,
            unearnedRevenue, otherAccruedExpenses, taxesPayable, outputVat,
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

        return accounts;
    }
}
