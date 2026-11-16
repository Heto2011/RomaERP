using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

/// <summary>The lowest quantity of an item a branch should hold. When the branch falls below it, an alert is raised (and a transfer suggested
/// from a branch holding more than it needs). Optional: an item with no minimum in a branch raises no alert.</summary>
public class BranchItemMinimum : AuditableEntity
{
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal MinimumQuantity { get; set; }
}
