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
