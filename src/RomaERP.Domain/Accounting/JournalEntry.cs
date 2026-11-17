using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public class JournalEntry : AuditableEntity
{
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public string? Description { get; set; }
    public string? Reference { get; set; }
    /// <summary>The branch this entry belongs to, when it came from a branch's own activity (a sale, stock written off). Empty = head office / not allocated.</summary>
    public Guid? WarehouseId { get; set; }
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();

    public decimal TotalDebit => Lines.Sum(l => l.Debit);
    public decimal TotalCredit => Lines.Sum(l => l.Credit);
    public bool IsBalanced => TotalDebit == TotalCredit;
}

public class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public int LineNumber { get; set; }

    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }

    /// <summary>The branch this line belongs to. Empty = the entry's own branch (or head office when the entry has none). Used by the
    /// month-end allocation of shared costs, whose single entry moves expense between head office and several branches.</summary>
    public Guid? WarehouseId { get; set; }
    /// <summary>True on the lines of a shared-cost allocation: the branches' share is shown as "shared costs", not as the branch's own spending.</summary>
    public bool IsCostAllocation { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}
