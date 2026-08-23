using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public class DepreciationRun : AuditableEntity
{
    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public DateTime RunDate { get; set; }
    public DepreciationRunStatus Status { get; set; } = DepreciationRunStatus.Draft;
    public string? Description { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<DepreciationRunLine> Lines { get; set; } = new List<DepreciationRunLine>();
}

public class DepreciationRunLine : BaseEntity
{
    public Guid DepreciationRunId { get; set; }
    public DepreciationRun? DepreciationRun { get; set; }

    public Guid FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }

    public decimal Amount { get; set; }
}
