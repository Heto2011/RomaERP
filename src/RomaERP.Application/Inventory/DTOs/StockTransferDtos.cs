namespace RomaERP.Application.Inventory.DTOs;

public class CreateStockTransferDto
{
    /// <summary>The sending branch ("Out").</summary>
    public Guid FromWarehouseId { get; set; }
    /// <summary>The receiving branch ("In").</summary>
    public Guid ToWarehouseId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Notes { get; set; }
    public List<CreateStockTransferLineDto> Lines { get; set; } = new();
}

public class CreateStockTransferLineDto
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Required only for lot-tracked items: which lot is being sent.</summary>
    public string? LotNumber { get; set; }
}

public class StockTransferLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    /// <summary>What the receiving branch counted (null until received).</summary>
    public decimal? ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? LotNumber { get; set; }
}

public class ReceivedLineDto
{
    public Guid ItemId { get; set; }
    public string? LotNumber { get; set; }
    public decimal ReceivedQuantity { get; set; }
}

public class ReceiveStockTransferDto
{
    public string? Note { get; set; }
    /// <summary>The counted quantity per line. Left out, everything is taken as received in full. A line below what was sent is a shortage.</summary>
    public List<ReceivedLineDto>? Lines { get; set; } = new();
}

public class StockTransferDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public Guid FromWarehouseId { get; set; }
    public string FromWarehouseName { get; set; } = string.Empty;
    public Guid ToWarehouseId { get; set; }
    public string ToWarehouseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? SentBy { get; set; }
    public DateTime SentAtUtc { get; set; }
    public string? ReceivedBy { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public string? ReceiveNote { get; set; }
    public decimal TotalCost { get; set; }
    public List<StockTransferLineDto> Lines { get; set; } = new();
}

public class WarehouseStockRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    /// <summary>What this branch holds now (derived from its stock movements).</summary>
    public decimal Quantity { get; set; }
    /// <summary>Stock on its way to this branch: sent by another branch, not yet received.</summary>
    public decimal IncomingQuantity { get; set; }
}
