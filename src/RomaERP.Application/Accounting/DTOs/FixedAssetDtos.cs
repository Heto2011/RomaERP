using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class FixedAssetDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid AssetAccountId { get; set; }
    public string AssetAccountCode { get; set; } = string.Empty;
    public string AssetAccountName { get; set; } = string.Empty;
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public string AccumulatedDepreciationAccountCode { get; set; } = string.Empty;
    public string AccumulatedDepreciationAccountName { get; set; } = string.Empty;
    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public int UsefulLifeYears { get; set; }
    public decimal SalvageValue { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public decimal? DecliningBalanceRate { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal BookValue { get; set; }
    public FixedAssetStatus Status { get; set; }
}

public class CreateFixedAssetDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public int UsefulLifeYears { get; set; }
    public decimal SalvageValue { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public decimal? DecliningBalanceRate { get; set; }
}
