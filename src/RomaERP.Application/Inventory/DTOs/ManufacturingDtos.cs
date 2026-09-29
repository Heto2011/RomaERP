namespace RomaERP.Application.Inventory.DTOs;

public class ManufacturingBomLineDto
{
    public Guid RawMaterialItemId { get; set; }
    public string RawMaterialItemCode { get; set; } = string.Empty;
    public string RawMaterialItemName { get; set; } = string.Empty;
    public decimal QuantityPerBatch { get; set; }
}

public class ManufacturingBomDto
{
    public Guid Id { get; set; }
    public Guid OutputItemId { get; set; }
    public string OutputItemCode { get; set; } = string.Empty;
    public string OutputItemName { get; set; } = string.Empty;
    public decimal OutputQuantity { get; set; }
    public List<ManufacturingBomLineDto> Lines { get; set; } = new();
}

public class CreateManufacturingBomLineDto
{
    public Guid RawMaterialItemId { get; set; }
    public decimal QuantityPerBatch { get; set; }
}

public class SetManufacturingBomDto
{
    public decimal OutputQuantity { get; set; }
    public List<CreateManufacturingBomLineDto> Lines { get; set; } = new();
}

public class ManufacturingOrderLineDto
{
    public Guid RawMaterialItemId { get; set; }
    public string RawMaterialItemCode { get; set; } = string.Empty;
    public string RawMaterialItemName { get; set; } = string.Empty;
    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class ManufacturingOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid OutputItemId { get; set; }
    public string OutputItemCode { get; set; } = string.Empty;
    public string OutputItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime ProductionDate { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public List<ManufacturingOrderLineDto> Lines { get; set; } = new();
}

public class CreateManufacturingOrderDto
{
    public Guid OutputItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime ProductionDate { get; set; }
    public decimal ProducedQuantity { get; set; }
    public string? Notes { get; set; }

    /// <summary>Required only when the output item is lot-tracked (Item.IsLotTracked).</summary>
    public string? OutputLotNumber { get; set; }
    public DateTime? OutputExpiryDate { get; set; }
}
