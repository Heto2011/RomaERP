using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

/// <summary>One received batch of a lot-tracked item, with its own remaining quantity and expiry date.
/// Consumption for a lot-tracked item draws down lots FEFO (earliest expiry first, nulls last) purely to
/// track what's on hand and expiring — it does not change how that consumption is costed (still the item's
/// weighted-average cost, unchanged).</summary>
public class ItemLot : AuditableEntity
{
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string LotNumber { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime ReceivedDate { get; set; }
}
