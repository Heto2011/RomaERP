using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

/// <summary>A manually-logged physical count for one item, snapshotting what the system said the balance was
/// at that moment against what was actually counted. This only records the variance for review — it does not
/// auto-adjust Item.QuantityOnHand; correcting the system's balance still goes through a normal stock
/// Issue/Receipt if the user decides to act on it.</summary>
public class PhysicalStockCount : AuditableEntity
{
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public DateTime CountDate { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}
