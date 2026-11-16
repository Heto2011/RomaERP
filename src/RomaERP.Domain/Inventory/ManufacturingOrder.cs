using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

/// <summary>One production run: consumes raw materials per a BOM (scaled to whatever quantity was actually
/// produced) and adds the resulting semi-finished item to inventory at its rolled-up cost. Pure inventory
/// transformation — internal control only, no GL posting (nothing was bought or sold), same as Purchase
/// Receiving. The output item's cost is realized later as COGS when a menu item using it is actually sold.</summary>
public class ManufacturingOrder : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid BomId { get; set; }
    public ManufacturingBom? Bom { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public DateTime ProductionDate { get; set; }

    /// <summary>How much of the output item this run actually produced (the BOM's OutputQuantity scaled by
    /// however many batches were made — need not be a whole multiple).</summary>
    public decimal ProducedQuantity { get; set; }

    /// <summary>Everything that becomes the produced quantity's cost: raw materials + conversion cost + absorbed fixed overhead (IAS 2).</summary>
    public decimal TotalCost { get; set; }
    /// <summary>Raw materials part of <see cref="TotalCost"/>.</summary>
    public decimal MaterialCost { get; set; }
    /// <summary>Direct labour and variable overhead part of the cost (the BOM's per-batch rate x batches).</summary>
    public decimal ConversionCost { get; set; }
    /// <summary>Fixed production overhead absorbed at the site's rate on normal capacity, never more than the site's monthly fixed overhead.</summary>
    public decimal FixedOverheadAllocated { get; set; }
    /// <summary>Batches this run counts for (produced quantity / BOM batch size).</summary>
    public decimal Batches { get; set; }
    public Guid? ConversionJournalEntryId { get; set; }

    public string? Notes { get; set; }

    public ICollection<ManufacturingOrderLine> Lines { get; set; } = new List<ManufacturingOrderLine>();
}

/// <summary>One raw material actually consumed by a production run, at its cost at that moment.</summary>
public class ManufacturingOrderLine : BaseEntity
{
    public Guid ManufacturingOrderId { get; set; }
    public ManufacturingOrder? ManufacturingOrder { get; set; }

    public Guid RawMaterialItemId { get; set; }
    public Item? RawMaterialItem { get; set; }

    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
