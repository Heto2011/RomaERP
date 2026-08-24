using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class FinancialReportService : IFinancialReportService
{
    private readonly IApplicationDbContext _context;

    public FinancialReportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IncomeStatementDto> GetIncomeStatementAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var lines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fromDate
                        && l.JournalEntry.EntryDate <= toDate
                        && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .ToListAsync(ct);

        var revenueLines = BuildLines(lines, AccountType.Revenue, creditPositive: true);
        var expenseLines = BuildLines(lines, AccountType.Expense, creditPositive: false);

        var totalRevenue = revenueLines.Sum(l => l.Amount);
        var totalExpense = expenseLines.Sum(l => l.Amount);

        return new IncomeStatementDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            RevenueLines = revenueLines,
            TotalRevenue = totalRevenue,
            ExpenseLines = expenseLines,
            TotalExpense = totalExpense,
            NetIncome = totalRevenue - totalExpense
        };
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate, CancellationToken ct = default)
    {
        var lines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate <= asOfDate
                        && (l.Account!.AccountType == AccountType.Asset
                            || l.Account.AccountType == AccountType.Liability
                            || l.Account.AccountType == AccountType.Equity))
            .ToListAsync(ct);

        var assetLines = BuildLines(lines, AccountType.Asset, creditPositive: false);
        var liabilityLines = BuildLines(lines, AccountType.Liability, creditPositive: true);
        var equityLines = BuildLines(lines, AccountType.Equity, creditPositive: true);

        var fiscalYear = await _context.FiscalYears
            .AsNoTracking()
            .Where(y => y.StartDate <= asOfDate && y.EndDate >= asOfDate)
            .FirstOrDefaultAsync(ct);

        var yearStart = fiscalYear?.StartDate ?? new DateTime(asOfDate.Year, 1, 1);
        var incomeStatement = await GetIncomeStatementAsync(yearStart, asOfDate, ct);

        var totalAssets = assetLines.Sum(l => l.Amount);
        var totalLiabilities = liabilityLines.Sum(l => l.Amount);
        var totalEquityExcludingNetIncome = equityLines.Sum(l => l.Amount);
        var totalEquity = totalEquityExcludingNetIncome + incomeStatement.NetIncome;
        var totalLiabilitiesAndEquity = totalLiabilities + totalEquity;

        return new BalanceSheetDto
        {
            AsOfDate = asOfDate,
            AssetLines = assetLines,
            TotalAssets = totalAssets,
            LiabilityLines = liabilityLines,
            TotalLiabilities = totalLiabilities,
            EquityLines = equityLines,
            CurrentYearNetIncome = incomeStatement.NetIncome,
            TotalEquity = totalEquity,
            TotalLiabilitiesAndEquity = totalLiabilitiesAndEquity,
            IsBalanced = totalAssets == totalLiabilitiesAndEquity
        };
    }

    public async Task<CostCenterAnalysisDto> GetCostCenterAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var lines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.CostCenter)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fromDate
                        && l.JournalEntry.EntryDate <= toDate
                        && (l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
            .ToListAsync(ct);

        var costCenters = lines
            .GroupBy(l => l.CostCenterId)
            .Select(g =>
            {
                var groupLines = g.ToList();
                var revenueBreakdown = BuildLines(groupLines, AccountType.Revenue, creditPositive: true);
                var expenseBreakdown = BuildLines(groupLines, AccountType.Expense, creditPositive: false);
                var totalRevenue = revenueBreakdown.Sum(l => l.Amount);
                var totalExpense = expenseBreakdown.Sum(l => l.Amount);
                var costCenter = g.Key.HasValue ? groupLines.First().CostCenter : null;

                return new CostCenterAnalysisLineDto
                {
                    CostCenterId = g.Key,
                    CostCenterCode = costCenter?.Code ?? string.Empty,
                    CostCenterName = costCenter?.NameAr ?? string.Empty,
                    RevenueBreakdown = revenueBreakdown,
                    TotalRevenue = totalRevenue,
                    ExpenseBreakdown = expenseBreakdown,
                    TotalExpense = totalExpense,
                    NetAmount = totalRevenue - totalExpense
                };
            })
            .Where(c => c.TotalRevenue != 0 || c.TotalExpense != 0)
            .OrderBy(c => c.CostCenterId.HasValue ? 0 : 1)
            .ThenBy(c => c.CostCenterCode)
            .ToList();

        return new CostCenterAnalysisDto { FromDate = fromDate, ToDate = toDate, CostCenters = costCenters };
    }

    private static List<ReportLineDto> BuildLines(List<JournalEntryLine> lines, AccountType type, bool creditPositive)
    {
        return lines
            .Where(l => l.Account!.AccountType == type)
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                var account = g.First().Account!;
                var debit = g.Sum(l => l.Debit);
                var credit = g.Sum(l => l.Credit);
                var amount = creditPositive ? credit - debit : debit - credit;

                return new ReportLineDto
                {
                    AccountCode = account.Code,
                    AccountName = account.NameAr,
                    Amount = amount
                };
            })
            .Where(l => l.Amount != 0)
            .OrderBy(l => l.AccountCode)
            .ToList();
    }
}
