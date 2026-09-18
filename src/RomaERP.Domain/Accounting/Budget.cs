using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

/// <summary>One line of a budget: how much a Revenue/Expense account is expected to move in a single
/// fiscal period (month). Actuals are computed on demand from JournalEntryLines — this table only stores
/// the target, never a snapshot of the actual.</summary>
public class Budget : AuditableEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public decimal Amount { get; set; }
}
