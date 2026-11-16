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
    public decimal ConversionCostPerBatch { get; set; }
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
    /// <summary>Direct labour and variable overhead for one batch.</summary>
    public decimal ConversionCostPerBatch { get; set; }
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
    public decimal MaterialCost { get; set; }
    public decimal ConversionCost { get; set; }
    public decimal FixedOverheadAllocated { get; set; }
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

public class ManufacturingSiteDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal MonthlyFixedOverhead { get; set; }
    public decimal NormalMonthlyBatches { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Fixed overhead put on one batch: monthly overhead / normal batches.</summary>
    public decimal RatePerBatch { get; set; }
}

public class SetManufacturingSiteDto
{
    public decimal MonthlyFixedOverhead { get; set; }
    public decimal NormalMonthlyBatches { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>For one site and month: what was absorbed into the cost of production against the site's fixed overhead, and what stays an expense of the period.</summary>
public class OverheadAbsorptionRowDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal MonthlyFixedOverhead { get; set; }
    public decimal NormalMonthlyBatches { get; set; }
    public decimal ActualBatches { get; set; }
    public decimal CapacityUsedPercent { get; set; }
    public decimal Absorbed { get; set; }
    /// <summary>Fixed overhead the production did not absorb because the site ran below normal capacity: an expense of the period, not part of stock cost.</summary>
    public decimal Unabsorbed { get; set; }
}
