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
}
