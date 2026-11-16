namespace RomaERP.Application.Inventory.DTOs;

public class CreateStockAdjustmentDto
{
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    /// <summary>True to add stock that is there but not in the books; false to take off stock that is missing.</summary>
    public bool IsIncrease { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>For an increase: the cost of one unit. Left out, the item's average cost is used.</summary>
    public decimal? UnitCost { get; set; }
    /// <summary>1 opening stock, 2 count gain, 3 count loss, 4 other.</summary>
    public int Reason { get; set; }
    public DateTime AdjustmentDate { get; set; }
    public string? Notes { get; set; }
    /// <summary>Only for lot-tracked items.</summary>
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    /// <summary>For a decrease: 1 branch, 2 company, 3 employee, 4 branch manager. Empty = the branch.</summary>
    public int? Responsibility { get; set; }
    public Guid? ResponsibleEmployeeId { get; set; }
}

public class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public bool IsIncrease { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public int Reason { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByName { get; set; }
}
