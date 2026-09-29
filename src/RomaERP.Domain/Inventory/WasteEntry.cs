using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

public enum WasteReason
{
    Waste = 1,
    Expired = 2,
    Damaged = 3,
    ProductionWaste = 4,
    OverPortion = 5,
    Unknown = 6
}

/// <summary>A real stock write-off — recording one creates a genuine Issue StockMovement (via
/// IInventoryService.IssueStockAsync), reducing QuantityOnHand and posting to the GL exactly like any other
/// stock issue. This entity just tags that movement with a waste Reason for reporting.</summary>
public class WasteEntry : AuditableEntity
{
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public DateTime WasteDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public WasteReason Reason { get; set; }
    public string? Notes { get; set; }

    public Guid StockMovementId { get; set; }
    public StockMovement? StockMovement { get; set; }
}
