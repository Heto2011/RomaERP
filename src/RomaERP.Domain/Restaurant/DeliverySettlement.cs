using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

/// <summary>An imported settlement statement from a delivery platform (Talabat, Careem, HungerStation, Jahez,
/// etc.) — there's no merchant API access to any of them, so this works the same way as bank reconciliation:
/// the user exports their own settlement report from the platform's dashboard and uploads it here. PlatformName
/// is free text since the set of platforms varies by country and changes over time.</summary>
public class DeliverySettlementImport : AuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string ImportedByUserId { get; set; } = string.Empty;

    public ICollection<DeliverySettlementLine> Lines { get; set; } = new List<DeliverySettlementLine>();
}

public class DeliverySettlementLine : BaseEntity
{
    public Guid DeliverySettlementImportId { get; set; }
    public DeliverySettlementImport? DeliverySettlementImport { get; set; }

    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
