namespace RomaERP.Application.HR.DTOs;

public class EmployeeDebtDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int Reason { get; set; }
    public string? Description { get; set; }
    public DateTime DebtDate { get; set; }
    public decimal Amount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal? MonthlyInstallment { get; set; }
    public int Status { get; set; }
    public string? SourceReference { get; set; }
    public string? CreatedByName { get; set; }
}

public class CreateAdvanceDto
{
    public Guid EmployeeId { get; set; }
    /// <summary>Required, no default: 1 advance, 4 external fine paid for the employee, 5 internal penalty.</summary>
    public int? Kind { get; set; }
    public decimal Amount { get; set; }
    public DateTime DebtDate { get; set; }
    public string? Description { get; set; }
    public decimal? MonthlyInstallment { get; set; }
    /// <summary>Where the money came from: "cash" (default) or "bank".</summary>
    public string? PaidFrom { get; set; }
}

public class UpdateDebtDto
{
    public decimal? MonthlyInstallment { get; set; }
    public bool Cancel { get; set; }
}

/// <summary>What the system suggests taking from one employee's pay for one debt in a payroll run; the accountant edits the amount before approving.</summary>
public class DebtDeductionProposalDto
{
    public Guid DebtId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal NetSalaryBefore { get; set; }
    public decimal ProposedAmount { get; set; }
    /// <summary>What is already approved for this run (if any).</summary>
    public decimal ApprovedAmount { get; set; }
}

public class ApproveDeductionItemDto
{
    public Guid DebtId { get; set; }
    public decimal Amount { get; set; }
}

public class ApproveDeductionsDto
{
    public List<ApproveDeductionItemDto> Items { get; set; } = new();
}
