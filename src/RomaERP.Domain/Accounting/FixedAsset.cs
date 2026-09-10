using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public class FixedAsset : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public Guid AssetAccountId { get; set; }
    public Account? AssetAccount { get; set; }

    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Account? AccumulatedDepreciationAccount { get; set; }

    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public int UsefulLifeYears { get; set; }
    public decimal SalvageValue { get; set; }

    public DepreciationMethod DepreciationMethod { get; set; }
    /// <summary>Annual rate applied to the remaining book value each period. Only used when DepreciationMethod is DecliningBalance.</summary>
    public decimal? DecliningBalanceRate { get; set; }

    public decimal AccumulatedDepreciation { get; set; }
    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Active;

    public decimal DepreciableBase => AcquisitionCost - SalvageValue;
    public decimal BookValue => AcquisitionCost - AccumulatedDepreciation;
}
