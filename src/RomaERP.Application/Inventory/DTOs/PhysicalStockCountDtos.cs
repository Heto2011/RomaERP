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
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? StockAdjustmentId { get; set; }
    /// <summary>0 recorded before approvals, 1 waiting for approval, 2 approved / no difference, 3 rejected.</summary>
    public int Status { get; set; }
    public int? Responsibility { get; set; }
    public string? ResponsibleEmployeeName { get; set; }
    public string? DecisionNote { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class ApproveCountDto
{
    /// <summary>For a shortage: 1 branch, 2 company, 3 employee, 4 branch manager.</summary>
    public int Responsibility { get; set; } = 1;
    public Guid? EmployeeId { get; set; }
    public string? Note { get; set; }
}

public class RejectCountDto
{
    public string? Note { get; set; }
}

public class CreatePhysicalStockCountDto
{
    public Guid ItemId { get; set; }
    public DateTime CountDate { get; set; }
    public decimal CountedQuantity { get; set; }
    public string? Notes { get; set; }
    /// <summary>The branch counted; its balance is what the count is compared with.</summary>
    public Guid? WarehouseId { get; set; }
}

public class CountVarianceRowDto
{
    public Guid? WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Counts { get; set; }
    public decimal ShortQuantity { get; set; }
    public decimal OverQuantity { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal NetValue { get; set; }
}
