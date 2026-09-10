using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public class PayrollRun : AuditableEntity
{
    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public DateTime RunDate { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public string? Description { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<PayrollRunLine> Lines { get; set; } = new List<PayrollRunLine>();
}

public class PayrollRunLine : BaseEntity
{
    public Guid PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }

    /// <summary>Informational only — already folded into <see cref="TotalDeductions"/>. Approved unpaid
    /// leave days (see EmployeeRequest) overlapping this run's fiscal period, and the amount deducted for
    /// them at BasicSalary ÷ CompanySettings.PayrollDaysPerMonth per day.</summary>
    public int UnpaidLeaveDays { get; set; }
    public decimal UnpaidLeaveDeductionAmount { get; set; }

    /// <summary>GOSI employee-side (Annuities) withholding — already folded into <see cref="TotalDeductions"/>
    /// — computed only when CompanySettings.GosiEnabled and the employee is marked IsSaudiNational.</summary>
    public decimal GosiEmployeeDeductionAmount { get; set; }
    /// <summary>GOSI employer-side (Annuities + Occupational Hazards) contribution — a company cost on top of
    /// the employee's pay, NOT part of TotalDeductions/NetSalary. Posted separately in PostAsync.</summary>
    public decimal GosiEmployerContributionAmount { get; set; }
}
