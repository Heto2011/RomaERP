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
