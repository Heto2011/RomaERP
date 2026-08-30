namespace RomaERP.Application.Accounting.DTOs;

public class ReportLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class IncomeStatementDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<ReportLineDto> RevenueLines { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public List<ReportLineDto> ExpenseLines { get; set; } = new();
    public decimal TotalExpense { get; set; }
    public decimal NetIncome { get; set; }
}

public class BalanceSheetDto
{
    public DateTime AsOfDate { get; set; }
    public List<ReportLineDto> AssetLines { get; set; } = new();
    public decimal TotalAssets { get; set; }
    public List<ReportLineDto> LiabilityLines { get; set; } = new();
    public decimal TotalLiabilities { get; set; }
    public List<ReportLineDto> EquityLines { get; set; } = new();
    public decimal CurrentYearNetIncome { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalLiabilitiesAndEquity { get; set; }
    public bool IsBalanced { get; set; }
}

/// <summary>One cost center's revenue/expense activity within the report period — including a synthetic
/// row (CostCenterId = null) for posted journal lines on Revenue/Expense accounts that carry no cost center
/// at all, so the report is honest about how much activity isn't actually being tracked by center.</summary>
public class CostCenterAnalysisLineDto
{
    public Guid? CostCenterId { get; set; }
    public string CostCenterCode { get; set; } = string.Empty;
    public string CostCenterName { get; set; } = string.Empty;
    public List<ReportLineDto> RevenueBreakdown { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public List<ReportLineDto> ExpenseBreakdown { get; set; } = new();
    public decimal TotalExpense { get; set; }
    public decimal NetAmount { get; set; }
}

public class CostCenterAnalysisDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<CostCenterAnalysisLineDto> CostCenters { get; set; } = new();
}

public class VatSummaryDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OutputVat { get; set; }
    public decimal InputVat { get; set; }
    public decimal NetVatPayable { get; set; }
}

public class CashFlowLineDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>Simplified direct-method cash flow: nets every posted entry's cash+bank lines and attributes the
/// movement to that entry's other (non-cash) account, so the reader sees "what was the money for" rather than
/// a formal operating/investing/financing split.</summary>
public class CashFlowStatementDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal BeginningCash { get; set; }
    public List<CashFlowLineDto> CashInLines { get; set; } = new();
    public decimal TotalCashIn { get; set; }
    public List<CashFlowLineDto> CashOutLines { get; set; } = new();
    public decimal TotalCashOut { get; set; }
    public decimal NetCashChange { get; set; }
    public decimal EndingCash { get; set; }
}

public class ItemProfitabilityLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

/// <summary>Covers only sales lines linked to an inventory Item (ItemId set) — recipe-based restaurant menu
/// items currently post as free-text revenue lines with no per-line item reference, so they aren't included
/// yet. Cost uses the item's *current* AverageCost as an approximation, since per-line COGS isn't snapshotted
/// at sale time (matches how COGS is already posted elsewhere in the system).</summary>
public class ItemProfitabilityReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<ItemProfitabilityLineDto> Items { get; set; } = new();
}

public class CustomerProfitabilityLineDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

/// <summary>Same coverage limitation as ItemProfitabilityReportDto: only invoice lines linked to an inventory
/// Item carry a cost, so a customer whose invoices are all recipe-based (restaurant) items will show revenue
/// with no attributed cost.</summary>
public class CustomerProfitabilityReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<CustomerProfitabilityLineDto> Customers { get; set; } = new();
}

public class SalesChannelProfitabilityLineDto
{
    public int Channel { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

/// <summary>Covers only billed Restaurant orders (DineIn/Takeaway/Delivery) — regular (non-restaurant) sales
/// invoices carry no channel concept in the current schema. Cost is real, not a placeholder: it's built from
/// each menu item's recipe (MenuRecipeLine — raw-material quantities) priced at the raw material's current
/// AverageCost, falling back to the menu item's own AverageCost when it has no recipe lines (sold as its own
/// raw material, e.g. a bottled drink).</summary>
public class SalesChannelProfitabilityReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<SalesChannelProfitabilityLineDto> Channels { get; set; } = new();
}

public class HistoricalMonthDto
{
    public string MonthLabel { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Expense { get; set; }
    public decimal NetIncome { get; set; }
}

public class ForecastMonthDto
{
    public string MonthLabel { get; set; } = string.Empty;
    public decimal ExpectedRevenue { get; set; }
    public decimal WorstRevenue { get; set; }
    public decimal BestRevenue { get; set; }
    public decimal ExpectedExpense { get; set; }
    public decimal ExpectedProfit { get; set; }
    public decimal WorstProfit { get; set; }
    public decimal BestProfit { get; set; }
}

/// <summary>A trend projection built from real posted monthly Income Statements — never a fabricated number.
/// With HistoricalMonthsUsed &lt; 3 there isn't enough history for a real trend or variance band, so the
/// projection flatlines at the last known month and the worst/best band is a fixed ±15% placeholder — that
/// low-confidence state is reported explicitly via IsLowConfidence rather than presented as a real forecast.
/// From 3 months on, ExpectedRevenue/ExpectedExpense extrapolate the average month-over-month growth rate,
/// and the worst/best band is ±1 standard deviation of the historical monthly revenue.</summary>
public class ForecastReportDto
{
    public int HistoricalMonthsUsed { get; set; }
    public bool IsLowConfidence { get; set; }
    public List<HistoricalMonthDto> HistoricalMonths { get; set; } = new();
    public List<ForecastMonthDto> ForecastMonths { get; set; } = new();
}

public class HiddenProfitLineDto
{
    public string ReasonCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>Sums up the real, currently-computable "leaks" between what the books say and what's actually
/// happening: physical stock count variance (PhysicalStockCount entries), waste write-offs (WasteEntry), and
/// items sold below their real cost (negative-margin lines from Item Profitability). Other leak sources from
/// the original concept (purchase price creep, cash/bank differences, discounts, commissions) aren't wired in
/// yet — they need data RomaERP doesn't capture today (a cash-count feature, bank/delivery reconciliation).
/// TotalImpact is negative when the net effect is a loss, which is the common case.</summary>
public class HiddenProfitReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<HiddenProfitLineDto> Lines { get; set; } = new();
    public decimal TotalImpact { get; set; }
}
