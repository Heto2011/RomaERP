using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.DTOs;

public class StockMovementDto
{
    public Guid Id { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public StockMovementType MovementType { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public class ReceiveStockDto
{
    public DateTime MovementDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }

    /// <summary>Required only when the item is lot-tracked (Item.IsLotTracked).</summary>
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class IssueStockDto
{
    public DateTime MovementDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? CostCenterId { get; set; }
    public decimal Quantity { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
}
