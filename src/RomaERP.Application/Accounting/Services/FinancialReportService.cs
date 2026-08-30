using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;
using RomaERP.Domain.HR;
using RomaERP.Domain.Restaurant;
using RomaERP.Domain.Inventory;

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

    public async Task<ForecastReportDto> GetForecastAsync(DateTime asOfDate, int historyMonths, int forecastMonths, CancellationToken ct = default)
    {
        var earliestEntryDate = await _context.JournalEntries
            .AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Posted && !e.IsDeleted)
            .OrderBy(e => e.EntryDate)
            .Select(e => (DateTime?)e.EntryDate)
            .FirstOrDefaultAsync(ct);

        if (earliestEntryDate is null)
            return new ForecastReportDto { HistoricalMonthsUsed = 0, IsLowConfidence = true };

        var asOfMonthStart = new DateTime(asOfDate.Year, asOfDate.Month, 1);
        var earliestMonthStart = new DateTime(earliestEntryDate.Value.Year, earliestEntryDate.Value.Month, 1);
        var requestedStart = asOfMonthStart.AddMonths(-(historyMonths - 1));
        var windowStart = requestedStart > earliestMonthStart ? requestedStart : earliestMonthStart;

        var historicalMonths = new List<HistoricalMonthDto>();
        for (var monthStart = windowStart; monthStart <= asOfMonthStart; monthStart = monthStart.AddMonths(1))
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var income = await GetIncomeStatementAsync(monthStart, monthEnd, ct);
            historicalMonths.Add(new HistoricalMonthDto
            {
                MonthLabel = monthStart.ToString("yyyy-MM"),
                Revenue = income.TotalRevenue,
                Expense = income.TotalExpense,
                NetIncome = income.NetIncome
            });
        }

        var isLowConfidence = historicalMonths.Count < 3;
        var lastRevenue = historicalMonths[^1].Revenue;
        var lastExpense = historicalMonths[^1].Expense;

        decimal avgRevenueGrowth = 0, avgExpenseGrowth = 0, revenueStdDev = 0;
        if (!isLowConfidence)
        {
            var revenueGrowths = new List<decimal>();
            var expenseGrowths = new List<decimal>();
            for (var i = 1; i < historicalMonths.Count; i++)
            {
                var prev = historicalMonths[i - 1];
                var curr = historicalMonths[i];
                revenueGrowths.Add(prev.Revenue != 0 ? (curr.Revenue - prev.Revenue) / prev.Revenue : 0);
                expenseGrowths.Add(prev.Expense != 0 ? (curr.Expense - prev.Expense) / prev.Expense : 0);
            }
            avgRevenueGrowth = revenueGrowths.Average();
            avgExpenseGrowth = expenseGrowths.Average();

            var meanRevenue = historicalMonths.Average(m => m.Revenue);
            var variance = historicalMonths.Sum(m => (m.Revenue - meanRevenue) * (m.Revenue - meanRevenue)) / historicalMonths.Count;
            revenueStdDev = (decimal)Math.Sqrt((double)variance);
        }

        var forecast = new List<ForecastMonthDto>();
        for (var j = 1; j <= forecastMonths; j++)
        {
            var monthLabel = asOfMonthStart.AddMonths(j).ToString("yyyy-MM");
            decimal expectedRevenue, expectedExpense, band;

            if (isLowConfidence)
            {
                expectedRevenue = lastRevenue;
                expectedExpense = lastExpense;
                band = Math.Abs(lastRevenue) * 0.15m;
            }
            else
            {
                expectedRevenue = lastRevenue * Pow(1 + avgRevenueGrowth, j);
                expectedExpense = lastExpense * Pow(1 + avgExpenseGrowth, j);
                band = revenueStdDev;
            }

            var worstRevenue = Math.Max(0, expectedRevenue - band);
            var bestRevenue = expectedRevenue + band;

            forecast.Add(new ForecastMonthDto
            {
                MonthLabel = monthLabel,
                ExpectedRevenue = Math.Round(expectedRevenue, 2),
                WorstRevenue = Math.Round(worstRevenue, 2),
                BestRevenue = Math.Round(bestRevenue, 2),
                ExpectedExpense = Math.Round(expectedExpense, 2),
                ExpectedProfit = Math.Round(expectedRevenue - expectedExpense, 2),
                WorstProfit = Math.Round(worstRevenue - expectedExpense, 2),
                BestProfit = Math.Round(bestRevenue - expectedExpense, 2)
            });
        }

        return new ForecastReportDto
        {
            HistoricalMonthsUsed = historicalMonths.Count,
            IsLowConfidence = isLowConfidence,
            HistoricalMonths = historicalMonths,
            ForecastMonths = forecast
        };
    }

    private static decimal Pow(decimal value, int exponent)
    {
        decimal result = 1;
        for (var i = 0; i < exponent; i++)
            result *= value;
        return result;
    }

    private const int CashFlowHistoryWeeks = 8;
    private const int CashFlowProjectionWeeks = 13;

    public async Task<CashFlowIntelligenceDto> GetCashFlowIntelligenceAsync(DateTime asOfDate, CancellationToken ct = default)
    {
        var cashCodes = new[] { AccountingConstants.CashOnHandAccountCode, AccountingConstants.BankAccountCode };

        var cashLines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted
                        && !l.JournalEntry.IsDeleted
                        && l.JournalEntry.EntryDate <= asOfDate
                        && cashCodes.Contains(l.Account!.Code))
            .Select(l => new { l.JournalEntry!.EntryDate, Net = l.Debit - l.Credit })
            .ToListAsync(ct);

        var currentCashBalance = cashLines.Sum(l => l.Net);

        DateTime WeekStart(DateTime d) => d.Date.AddDays(-(int)d.Date.DayOfWeek);
        var currentWeekStart = WeekStart(asOfDate);

        var historicalWeeks = new List<decimal>();
        for (var w = 1; w <= CashFlowHistoryWeeks; w++)
        {
            var weekStart = currentWeekStart.AddDays(-7 * w);
            var weekEnd = weekStart.AddDays(6);
            var weekNet = cashLines.Where(l => l.EntryDate.Date >= weekStart && l.EntryDate.Date <= weekEnd).Sum(l => l.Net);
            historicalWeeks.Add(weekNet);
        }
        historicalWeeks.Reverse();

        var weeksWithActivity = cashLines.Select(l => WeekStart(l.EntryDate)).Distinct().Count();
        var isLowConfidence = weeksWithActivity < 4;
        var averageWeeklyNetChange = historicalWeeks.Count > 0 ? historicalWeeks.Average() : 0;

        var projectedWeeks = new List<CashFlowProjectedWeekDto>();
        var runningBalance = currentCashBalance;
        DateTime? firstNegativeWeek = null;
        for (var w = 1; w <= CashFlowProjectionWeeks; w++)
        {
            var weekStart = currentWeekStart.AddDays(7 * w);
            runningBalance += averageWeeklyNetChange;
            var isBelowZero = runningBalance < 0;
            if (isBelowZero && firstNegativeWeek is null)
                firstNegativeWeek = weekStart;

            projectedWeeks.Add(new CashFlowProjectedWeekDto
            {
                WeekStart = weekStart,
                ProjectedNetChange = Math.Round(averageWeeklyNetChange, 2),
                ProjectedEndingBalance = Math.Round(runningBalance, 2),
                IsBelowZero = isBelowZero
            });
        }

        return new CashFlowIntelligenceDto
        {
            AsOfDate = asOfDate,
            CurrentCashBalance = Math.Round(currentCashBalance, 2),
            HistoricalWeeksUsed = weeksWithActivity,
            IsLowConfidence = isLowConfidence,
            AverageWeeklyNetChange = Math.Round(averageWeeklyNetChange, 2),
            ProjectedWeeks = projectedWeeks,
            FirstWeekBelowZero = firstNegativeWeek
        };
    }

    public async Task<HiddenProfitReportDto> GetHiddenProfitAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var stockVarianceValue = await _context.PhysicalStockCounts
            .AsNoTracking()
            .Where(c => c.CountDate >= fromDate && c.CountDate <= toDate)
            .SumAsync(c => (c.CountedQuantity - c.SystemQuantity) * c.UnitCost, ct);

        var wasteCost = await _context.WasteEntries
            .AsNoTracking()
            .Where(w => w.WasteDate >= fromDate && w.WasteDate <= toDate)
            .SumAsync(w => w.TotalCost, ct);

        var itemProfitability = await GetItemProfitabilityAsync(fromDate, toDate, ct);
        var belowCostLoss = itemProfitability.Items.Where(i => i.GrossProfit < 0).Sum(i => i.GrossProfit);

        var lines = new List<HiddenProfitLineDto>
        {
            new() { ReasonCode = "stock-variance", Amount = Math.Round(stockVarianceValue, 2) },
            new() { ReasonCode = "waste", Amount = Math.Round(-wasteCost, 2) },
            new() { ReasonCode = "below-cost", Amount = Math.Round(belowCostLoss, 2) }
        };

        return new HiddenProfitReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            Lines = lines,
            TotalImpact = lines.Sum(l => l.Amount)
        };
    }

    public async Task<LaborReportDto> GetLaborReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var totalPayroll = await _context.PayrollRunLines
            .AsNoTracking()
            .Include(l => l.PayrollRun)
            .Where(l => l.PayrollRun!.Status == PayrollRunStatus.Posted
                        && l.PayrollRun.RunDate >= fromDate
                        && l.PayrollRun.RunDate <= toDate)
            .SumAsync(l => l.NetSalary, ct);

        var income = await GetIncomeStatementAsync(fromDate, toDate, ct);
        var totalSalesRevenue = income.TotalRevenue;

        var billedOrders = await _context.RestaurantOrders
            .AsNoTracking()
            .Include(o => o.WaiterEmployee)
            .Include(o => o.CashierShift!).ThenInclude(s => s.Employee)
            .Include(o => o.SalesInvoice)
            .Where(o => o.Status == RestaurantOrderStatus.Billed
                        && o.OrderDate >= fromDate
                        && o.OrderDate <= toDate)
            .ToListAsync(ct);

        var salesByEmployee = billedOrders
            .Select(o => new { Employee = o.WaiterEmployee ?? o.CashierShift?.Employee, Amount = o.SalesInvoice?.SubTotal ?? 0 })
            .Where(x => x.Employee != null)
            .GroupBy(x => x.Employee!.Id)
            .Select(g => new EmployeeSalesLineDto
            {
                EmployeeId = g.Key,
                EmployeeName = g.First().Employee!.FullNameAr,
                SalesTotal = g.Sum(x => x.Amount),
                OrderCount = g.Count()
            })
            .OrderByDescending(l => l.SalesTotal)
            .ToList();

        return new LaborReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalPayroll = totalPayroll,
            TotalSalesRevenue = totalSalesRevenue,
            LaborCostPercent = totalSalesRevenue != 0 ? Math.Round(totalPayroll / totalSalesRevenue * 100, 2) : null,
            SalesByEmployee = salesByEmployee
        };
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
