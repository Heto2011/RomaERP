using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class BudgetLineDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class SetBudgetLineDto
{
    public Guid AccountId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public decimal Amount { get; set; }
}

public class BudgetVsActualLineDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    /// <summary>Null when there's no budget set for this account (division by zero avoided).</summary>
    public decimal? VariancePercent { get; set; }
}

public class BudgetVsActualReportDto
{
    public Guid FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;
    public List<BudgetVsActualLineDto> RevenueLines { get; set; } = new();
    public List<BudgetVsActualLineDto> ExpenseLines { get; set; } = new();
    public decimal TotalBudgetedRevenue { get; set; }
    public decimal TotalActualRevenue { get; set; }
    public decimal TotalBudgetedExpense { get; set; }
    public decimal TotalActualExpense { get; set; }
    public decimal BudgetedNetIncome { get; set; }
    public decimal ActualNetIncome { get; set; }
}
