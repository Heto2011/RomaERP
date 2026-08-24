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
}
