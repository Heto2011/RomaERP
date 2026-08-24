using RomaERP.Domain.Common;
using RomaERP.Domain.Restaurant;

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

    /// <summary>Set when this item appears on the restaurant/POS menu (either sold directly, or as the
    /// finished product of a recipe — see RecipeLines).</summary>
    public bool IsMenuItem { get; set; }
    public decimal MenuPrice { get; set; }

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();

    /// <summary>Ingredients consumed when this item (as a menu item) is sold. Empty means this item is its
    /// own raw material — sold and decremented directly.</summary>
    public ICollection<MenuRecipeLine> RecipeLines { get; set; } = new List<MenuRecipeLine>();
}
