using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.Restaurant;

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

    public async Task<VatSummaryDto> GetVatSummaryAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var lines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fromDate
                        && l.JournalEntry.EntryDate <= toDate
                        && (l.Account!.Code == AccountingConstants.OutputVatAccountCode
                            || l.Account.Code == AccountingConstants.InputVatAccountCode))
            .ToListAsync(ct);

        var outputVat = lines.Where(l => l.Account!.Code == AccountingConstants.OutputVatAccountCode).Sum(l => l.Credit - l.Debit);
        var inputVat = lines.Where(l => l.Account!.Code == AccountingConstants.InputVatAccountCode).Sum(l => l.Debit - l.Credit);

        return new VatSummaryDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            OutputVat = outputVat,
            InputVat = inputVat,
            NetVatPayable = outputVat - inputVat
        };
    }

    public async Task<CashFlowStatementDto> GetCashFlowStatementAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var cashCodes = new[] { AccountingConstants.CashOnHandAccountCode, AccountingConstants.BankAccountCode };

        var beginningLines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate < fromDate
                        && cashCodes.Contains(l.Account!.Code))
            .ToListAsync(ct);
        var beginningCash = beginningLines.Sum(l => l.Debit - l.Credit);

        var periodCashLines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate >= fromDate
                        && l.JournalEntry.EntryDate <= toDate
                        && cashCodes.Contains(l.Account!.Code))
            .ToListAsync(ct);

        var entryIds = periodCashLines.Select(l => l.JournalEntryId).Distinct().ToList();
        var linesByEntry = (await _context.JournalEntryLines
                .AsNoTracking()
                .Include(l => l.Account)
                .Where(l => entryIds.Contains(l.JournalEntryId))
                .ToListAsync(ct))
            .GroupBy(l => l.JournalEntryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var categoryTotals = new Dictionary<string, (string Name, decimal Amount)>();
        foreach (var group in periodCashLines.GroupBy(l => l.JournalEntryId))
        {
            var netCashDelta = group.Sum(l => l.Debit - l.Credit);
            if (netCashDelta == 0) continue;

            var counterLine = linesByEntry[group.Key].FirstOrDefault(l => !cashCodes.Contains(l.Account!.Code));
            var categoryCode = counterLine?.Account?.Code ?? "OTHER";
            var categoryName = counterLine?.Account?.NameAr ?? "أخرى";

            categoryTotals.TryGetValue(categoryCode, out var existing);
            categoryTotals[categoryCode] = (categoryName, existing.Amount + netCashDelta);
        }

        var cashInLines = categoryTotals
            .Where(kv => kv.Value.Amount > 0)
            .Select(kv => new CashFlowLineDto { CategoryCode = kv.Key, CategoryName = kv.Value.Name, Amount = kv.Value.Amount })
            .OrderByDescending(l => l.Amount)
            .ToList();
        var cashOutLines = categoryTotals
            .Where(kv => kv.Value.Amount < 0)
            .Select(kv => new CashFlowLineDto { CategoryCode = kv.Key, CategoryName = kv.Value.Name, Amount = -kv.Value.Amount })
            .OrderByDescending(l => l.Amount)
            .ToList();

        var totalIn = cashInLines.Sum(l => l.Amount);
        var totalOut = cashOutLines.Sum(l => l.Amount);

        return new CashFlowStatementDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            BeginningCash = beginningCash,
            CashInLines = cashInLines,
            TotalCashIn = totalIn,
            CashOutLines = cashOutLines,
            TotalCashOut = totalOut,
            NetCashChange = totalIn - totalOut,
            EndingCash = beginningCash + totalIn - totalOut
        };
    }

    public async Task<ItemProfitabilityReportDto> GetItemProfitabilityAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var lines = await _context.SalesInvoiceLines
            .AsNoTracking()
            .Include(l => l.Item)
            .Include(l => l.SalesInvoice)
            .Where(l => l.ItemId != null
                        && l.SalesInvoice!.InvoiceDate >= fromDate
                        && l.SalesInvoice.InvoiceDate <= toDate)
            .ToListAsync(ct);

        var items = lines
            .GroupBy(l => l.ItemId!.Value)
            .Select(g =>
            {
                var item = g.First().Item!;
                var quantity = g.Sum(l => l.Quantity);
                var revenue = g.Sum(l => l.LineTotal);
                var cost = Math.Round(quantity * item.AverageCost, 2);
                var grossProfit = revenue - cost;

                return new ItemProfitabilityLineDto
                {
                    ItemId = item.Id,
                    ItemCode = item.Code,
                    ItemName = item.NameAr,
                    QuantitySold = quantity,
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = grossProfit,
                    MarginPercent = revenue != 0 ? Math.Round(grossProfit / revenue * 100, 2) : 0
                };
            })
            .OrderByDescending(l => l.GrossProfit)
            .ToList();

        return new ItemProfitabilityReportDto { FromDate = fromDate, ToDate = toDate, Items = items };
    }

    public async Task<CustomerProfitabilityReportDto> GetCustomerProfitabilityAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var lines = await _context.SalesInvoiceLines
            .AsNoTracking()
            .Include(l => l.Item)
            .Include(l => l.SalesInvoice)
                .ThenInclude(i => i!.Customer)
            .Where(l => l.ItemId != null
                        && l.SalesInvoice!.InvoiceDate >= fromDate
                        && l.SalesInvoice.InvoiceDate <= toDate)
            .ToListAsync(ct);

        var customers = lines
            .GroupBy(l => l.SalesInvoice!.CustomerId)
            .Select(g =>
            {
                var customer = g.First().SalesInvoice!.Customer!;
                var revenue = g.Sum(l => l.LineTotal);
                var cost = g.Sum(l => Math.Round(l.Quantity * l.Item!.AverageCost, 2));
                var grossProfit = revenue - cost;

                return new CustomerProfitabilityLineDto
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.NameAr,
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = grossProfit,
                    MarginPercent = revenue != 0 ? Math.Round(grossProfit / revenue * 100, 2) : 0
                };
            })
            .OrderByDescending(l => l.GrossProfit)
            .ToList();

        return new CustomerProfitabilityReportDto { FromDate = fromDate, ToDate = toDate, Customers = customers };
    }

    public async Task<SalesChannelProfitabilityReportDto> GetSalesChannelProfitabilityAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var orderLines = await _context.RestaurantOrderLines
            .AsNoTracking()
            .Include(l => l.RestaurantOrder)
            .Where(l => l.RestaurantOrder!.Status == RestaurantOrderStatus.Billed
                        && l.RestaurantOrder.OrderDate >= fromDate
                        && l.RestaurantOrder.OrderDate <= toDate)
            .ToListAsync(ct);

        var menuItemIds = orderLines.Select(l => l.ItemId).Distinct().ToList();

        var recipeLines = await _context.MenuRecipeLines
            .AsNoTracking()
            .Include(r => r.RawMaterialItem)
            .Where(r => menuItemIds.Contains(r.MenuItemId))
            .ToListAsync(ct);
        var recipeCostByMenuItem = recipeLines
            .GroupBy(r => r.MenuItemId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.QuantityPerUnit * r.RawMaterialItem!.AverageCost));

        var itemsById = (await _context.Items
                .AsNoTracking()
                .Where(i => menuItemIds.Contains(i.Id))
                .ToListAsync(ct))
            .ToDictionary(i => i.Id);

        decimal UnitCost(Guid menuItemId) =>
            recipeCostByMenuItem.TryGetValue(menuItemId, out var recipeCost)
                ? recipeCost
                : itemsById.TryGetValue(menuItemId, out var item) ? item.AverageCost : 0;

        var channels = orderLines
            .GroupBy(l => l.RestaurantOrder!.OrderType)
            .Select(g =>
            {
                var revenue = g.Sum(l => l.LineTotal);
                var cost = g.Sum(l => Math.Round(l.Quantity * UnitCost(l.ItemId), 2));
                var grossProfit = revenue - cost;

                return new SalesChannelProfitabilityLineDto
                {
                    Channel = (int)g.Key,
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = grossProfit,
                    MarginPercent = revenue != 0 ? Math.Round(grossProfit / revenue * 100, 2) : 0
                };
            })
            .OrderByDescending(l => l.GrossProfit)
            .ToList();

        return new SalesChannelProfitabilityReportDto { FromDate = fromDate, ToDate = toDate, Channels = channels };
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
