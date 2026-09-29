using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

/// <summary>A production recipe (Bill of Materials): how much raw material it takes to produce one batch of
/// a semi-finished/intermediate item (e.g. 20kg of tomato sauce from 18kg tomatoes + 2kg spices), so that
/// item can then be used inside a menu item's recipe like any other ingredient.</summary>
public class ManufacturingBom : AuditableEntity
{
    public Guid OutputItemId { get; set; }
    public Item? OutputItem { get; set; }

    /// <summary>How much of OutputItem one full production run of this BOM yields (e.g. 20 kg).</summary>
    public decimal OutputQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ManufacturingBomLine> Lines { get; set; } = new List<ManufacturingBomLine>();
}

/// <summary>One raw material consumed per full batch (OutputQuantity) of the parent BOM.</summary>
public class ManufacturingBomLine : BaseEntity
{
    public Guid BomId { get; set; }
    public ManufacturingBom? Bom { get; set; }

    public Guid RawMaterialItemId { get; set; }
    public Item? RawMaterialItem { get; set; }

    public decimal QuantityPerBatch { get; set; }
}
