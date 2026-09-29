namespace RomaERP.Application.Inventory.DTOs;

public class PhysicalStockCountDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Variance { get; set; }
    public decimal UnitCost { get; set; }
    public decimal VarianceValue { get; set; }
    public string? Notes { get; set; }
}

public class CreatePhysicalStockCountDto
{
    public Guid ItemId { get; set; }
    public DateTime CountDate { get; set; }
    public decimal CountedQuantity { get; set; }
    public string? Notes { get; set; }
}
