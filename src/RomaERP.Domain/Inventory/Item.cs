using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

public class Item : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;

    public Guid ItemCategoryId { get; set; }
    public ItemCategory? ItemCategory { get; set; }

    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Running quantity on hand across all warehouses, maintained by InventoryService on each posted movement.</summary>
    public decimal QuantityOnHand { get; set; }

    /// <summary>Weighted-average unit cost, recalculated on each stock receipt.</summary>
    public decimal AverageCost { get; set; }

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}
