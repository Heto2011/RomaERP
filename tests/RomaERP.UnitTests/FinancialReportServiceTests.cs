using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Domain.Accounting;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class FinancialReportServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetCostCenterAnalysis_GroupsPostedLinesByCostCenterIncludingUnassignedBucket()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var expense = new Account { Code = "5300", NameAr = "مصروفات إدارية", NameEn = "Admin Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };

        var branchA = new CostCenter { Code = "CC-A", NameAr = "فرع أ", NameEn = "Branch A" };
        var branchB = new CostCenter { Code = "CC-B", NameAr = "فرع ب", NameEn = "Branch B" };

        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "August", PeriodNumber = 8, StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2026, 8, 31) };

        ctx.Accounts.AddRange(cash, revenue, expense);
        ctx.CostCenters.AddRange(branchA, branchB);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        var inRangeEntry = new JournalEntry
        {
            EntryNumber = "JV-000001",
            EntryDate = new DateTime(2026, 8, 15),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = cash.Id, Debit = 300, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 1000, CostCenterId = branchA.Id },
                new JournalEntryLine { LineNumber = 3, AccountId = expense.Id, Debit = 400, Credit = 0, CostCenterId = branchA.Id },
                new JournalEntryLine { LineNumber = 4, AccountId = expense.Id, Debit = 200, Credit = 0, CostCenterId = branchB.Id },
                new JournalEntryLine { LineNumber = 5, AccountId = expense.Id, Debit = 100, Credit = 0, CostCenterId = null }
            }
        };

        // Should be excluded: outside the requested date range.
        var outOfRangeEntry = new JournalEntry
        {
            EntryNumber = "JV-000002",
            EntryDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines = { new JournalEntryLine { LineNumber = 1, AccountId = expense.Id, Debit = 9999, Credit = 0, CostCenterId = branchA.Id } }
        };

        // Should be excluded: not posted.
        var draftEntry = new JournalEntry
        {
            EntryNumber = "JV-000003",
            EntryDate = new DateTime(2026, 8, 20),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Draft,
            Lines = { new JournalEntryLine { LineNumber = 1, AccountId = expense.Id, Debit = 5000, Credit = 0, CostCenterId = branchA.Id } }
        };

        ctx.JournalEntries.AddRange(inRangeEntry, outOfRangeEntry, draftEntry);
        await ctx.SaveChangesAsync();

        var service = new FinancialReportService(ctx);
        var report = await service.GetCostCenterAnalysisAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        Assert.Equal(3, report.CostCenters.Count);

        var a = report.CostCenters.Single(c => c.CostCenterId == branchA.Id);
        Assert.Equal("CC-A", a.CostCenterCode);
        Assert.Equal(1000, a.TotalRevenue);
        Assert.Equal(400, a.TotalExpense);
        Assert.Equal(600, a.NetAmount);

        var b = report.CostCenters.Single(c => c.CostCenterId == branchB.Id);
        Assert.Equal(0, b.TotalRevenue);
        Assert.Equal(200, b.TotalExpense);
        Assert.Equal(-200, b.NetAmount);

        var unassigned = report.CostCenters.Single(c => c.CostCenterId == null);
        Assert.Equal(0, unassigned.TotalRevenue);
        Assert.Equal(100, unassigned.TotalExpense);
        Assert.Equal(-100, unassigned.NetAmount);

        // Assigned cost centers sort before the unassigned bucket.
        Assert.NotNull(report.CostCenters[0].CostCenterId);
        Assert.NotNull(report.CostCenters[1].CostCenterId);
        Assert.Null(report.CostCenters[2].CostCenterId);
    }

    [Fact]
    public async Task GetCostCenterAnalysis_NoPostedActivity_ReturnsEmptyList()
    {
        var ctx = CreateContext();
        var service = new FinancialReportService(ctx);

        var report = await service.GetCostCenterAnalysisAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.Empty(report.CostCenters);
    }

    [Fact]
    public async Task GetVatSummary_NetsOutputAgainstInputAcrossPostedEntriesInRange()
    {
        var ctx = CreateContext();

        var ar = new Account { Code = "1120", NameAr = "عملاء", NameEn = "AR", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var outputVat = new Account { Code = "2161", NameAr = "ضريبة مخرجات", NameEn = "Output VAT", AccountType = AccountType.Liability, Nature = AccountNature.Credit };
        var inventory = new Account { Code = "1160", NameAr = "المخزون", NameEn = "Inventory", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var inputVat = new Account { Code = "1180", NameAr = "ضريبة مدخلات", NameEn = "Input VAT", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var ap = new Account { Code = "2120", NameAr = "موردون", NameEn = "AP", AccountType = AccountType.Liability, Nature = AccountNature.Credit };

        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "August", PeriodNumber = 8, StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2026, 8, 31) };

        ctx.Accounts.AddRange(ar, revenue, outputVat, inventory, inputVat, ap);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        var sale = new JournalEntry
        {
            EntryNumber = "JV-000001",
            EntryDate = new DateTime(2026, 8, 5),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = ar.Id, Debit = 1150, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 1000 },
                new JournalEntryLine { LineNumber = 3, AccountId = outputVat.Id, Debit = 0, Credit = 150 }
            }
        };
        var creditNote = new JournalEntry
        {
            EntryNumber = "JV-000002",
            EntryDate = new DateTime(2026, 8, 10),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = outputVat.Id, Debit = 15, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = ar.Id, Debit = 0, Credit = 15 }
            }
        };
        var purchase = new JournalEntry
        {
            EntryNumber = "JV-000003",
            EntryDate = new DateTime(2026, 8, 12),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = inventory.Id, Debit = 400, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = inputVat.Id, Debit = 60, Credit = 0 },
                new JournalEntryLine { LineNumber = 3, AccountId = ap.Id, Debit = 0, Credit = 460 }
            }
        };
        var purchaseReturn = new JournalEntry
        {
            EntryNumber = "JV-000004",
            EntryDate = new DateTime(2026, 8, 14),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = ap.Id, Debit = 46, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = inputVat.Id, Debit = 0, Credit = 10 },
                new JournalEntryLine { LineNumber = 3, AccountId = inventory.Id, Debit = 0, Credit = 36 }
            }
        };
        // Should be excluded: outside the requested date range.
        var outOfRangeSale = new JournalEntry
        {
            EntryNumber = "JV-000005",
            EntryDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines = { new JournalEntryLine { LineNumber = 1, AccountId = outputVat.Id, Debit = 0, Credit = 9999 } }
        };
        // Should be excluded: not posted.
        var draftSale = new JournalEntry
        {
            EntryNumber = "JV-000006",
            EntryDate = new DateTime(2026, 8, 20),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Draft,
            Lines = { new JournalEntryLine { LineNumber = 1, AccountId = outputVat.Id, Debit = 0, Credit = 9999 } }
        };

        ctx.JournalEntries.AddRange(sale, creditNote, purchase, purchaseReturn, outOfRangeSale, draftSale);
        await ctx.SaveChangesAsync();

        var service = new FinancialReportService(ctx);
        var report = await service.GetVatSummaryAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        Assert.Equal(135, report.OutputVat);
        Assert.Equal(50, report.InputVat);
        Assert.Equal(85, report.NetVatPayable);
    }

    [Fact]
    public async Task GetCashFlowStatement_NetsCashLinesPerEntryAndCategorizesByCounterAccount()
    {
        var ctx = CreateContext();

        var cash = new Account { Code = "1111", NameAr = "الصندوق", NameEn = "Cash", AccountType = AccountType.Asset, Nature = AccountNature.Debit };
        var revenue = new Account { Code = "4100", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.Credit };
        var rentExpense = new Account { Code = "5300", NameAr = "مصروف إيجار", NameEn = "Rent Expense", AccountType = AccountType.Expense, Nature = AccountNature.Debit };

        var year = new FiscalYear { Name = "2026", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31) };
        var period = new FiscalPeriod { FiscalYear = year, FiscalYearId = year.Id, Name = "August", PeriodNumber = 8, StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2026, 8, 31) };

        ctx.Accounts.AddRange(cash, revenue, rentExpense);
        ctx.FiscalYears.Add(year);
        ctx.FiscalPeriods.Add(period);
        await ctx.SaveChangesAsync();

        var beforePeriod = new JournalEntry
        {
            EntryNumber = "JV-000001",
            EntryDate = new DateTime(2026, 7, 15),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = cash.Id, Debit = 5000, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 5000 }
            }
        };
        var sale1 = new JournalEntry
        {
            EntryNumber = "JV-000002",
            EntryDate = new DateTime(2026, 8, 3),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = cash.Id, Debit = 1000, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 1000 }
            }
        };
        var sale2 = new JournalEntry
        {
            EntryNumber = "JV-000003",
            EntryDate = new DateTime(2026, 8, 9),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = cash.Id, Debit = 500, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 500 }
            }
        };
        var rentPaid = new JournalEntry
        {
            EntryNumber = "JV-000004",
            EntryDate = new DateTime(2026, 8, 15),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = rentExpense.Id, Debit = 300, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = cash.Id, Debit = 0, Credit = 300 }
            }
        };
        // Should be excluded: not posted.
        var draftCashEntry = new JournalEntry
        {
            EntryNumber = "JV-000005",
            EntryDate = new DateTime(2026, 8, 20),
            FiscalPeriodId = period.Id,
            Status = JournalEntryStatus.Draft,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = cash.Id, Debit = 999, Credit = 0 },
                new JournalEntryLine { LineNumber = 2, AccountId = revenue.Id, Debit = 0, Credit = 999 }
            }
        };

        ctx.JournalEntries.AddRange(beforePeriod, sale1, sale2, rentPaid, draftCashEntry);
        await ctx.SaveChangesAsync();

        var service = new FinancialReportService(ctx);
        var report = await service.GetCashFlowStatementAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        Assert.Equal(5000, report.BeginningCash);

        var inLine = Assert.Single(report.CashInLines);
        Assert.Equal("4100", inLine.CategoryCode);
        Assert.Equal(1500, inLine.Amount);
        Assert.Equal(1500, report.TotalCashIn);

        var outLine = Assert.Single(report.CashOutLines);
        Assert.Equal("5300", outLine.CategoryCode);
        Assert.Equal(300, outLine.Amount);
        Assert.Equal(300, report.TotalCashOut);

        Assert.Equal(1200, report.NetCashChange);
        Assert.Equal(6200, report.EndingCash);
    }
}
