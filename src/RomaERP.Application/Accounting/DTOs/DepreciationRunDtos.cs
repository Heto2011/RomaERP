using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class DepreciationRunLineDto
{
    public Guid FixedAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class DepreciationRunDto
{
    public Guid Id { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public DateTime RunDate { get; set; }
    public DepreciationRunStatus Status { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
    public List<DepreciationRunLineDto> Lines { get; set; } = new();
    public decimal TotalAmount => Lines.Sum(l => l.Amount);
}

public class CreateDepreciationRunDto
{
    public Guid FiscalPeriodId { get; set; }
    public DateTime RunDate { get; set; }
    public string? Description { get; set; }
}
