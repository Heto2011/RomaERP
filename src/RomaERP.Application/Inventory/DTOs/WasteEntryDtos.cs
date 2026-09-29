namespace RomaERP.Application.Inventory.DTOs;

public class WasteEntryDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime WasteDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public int Reason { get; set; }
    public string? Notes { get; set; }
}

public class CreateWasteEntryDto
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public DateTime WasteDate { get; set; }
    public decimal Quantity { get; set; }
    public int Reason { get; set; }
    public string? Notes { get; set; }
}
