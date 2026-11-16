using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

/// <summary>Why an employee owes the company. Advance = money the company gave. StockShortage = stock found short, charged to them.
/// ExternalFine = an outside fine (traffic, authority) the company PAID for them, so it is owed back. InternalPenalty = a disciplinary penalty
/// the company imposes: nothing was paid out, so it is simply taken off pay and lowers the salaries expense.</summary>
public enum EmployeeDebtReason { Advance = 1, StockShortage = 2, Other = 3, ExternalFine = 4, InternalPenalty = 5 }
public enum EmployeeDebtStatus { Active = 1, Settled = 2, Cancelled = 3 }

/// <summary>Money an employee owes the company, whatever its source (a salary advance, stock found short, ...). One ledger for all of them:
/// it is repaid by deductions from pay, in instalments if set, each approved by the owner or accountant before payroll posts.</summary>
public class EmployeeDebt : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public EmployeeDebtReason Reason { get; set; }
    public string? Description { get; set; }
    public DateTime DebtDate { get; set; }
    public decimal Amount { get; set; }
    public decimal RemainingAmount { get; set; }
    /// <summary>Per payroll run. Empty = proposed in full at the next run.</summary>
    public decimal? MonthlyInstallment { get; set; }
    public EmployeeDebtStatus Status { get; set; } = EmployeeDebtStatus.Active;
    /// <summary>What it came from, e.g. the number of the stock adjustment.</summary>
    public string? SourceReference { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>An approved deduction of part of a debt from one payroll run. It reduces the debt only when that run is posted.</summary>
public class EmployeeDebtDeduction : AuditableEntity
{
    public Guid EmployeeDebtId { get; set; }
    public EmployeeDebt? EmployeeDebt { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public string? ApprovedBy { get; set; }
    public bool IsPosted { get; set; }
}
