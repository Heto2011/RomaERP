using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

public enum StockTransferStatus
{
    /// <summary>Sent ("Out") from the first branch, not yet received ("In") by the second.</summary>
    InTransit = 1,
    Received = 2,
    /// <summary>Called back by the sender before it was received: the stock went back to the sending branch.</summary>
    Cancelled = 3
}

/// <summary>Moves stock from one branch (warehouse) to another inside the same company. It has two steps: the sending branch records
/// Out, the receiving branch records In. It never touches the books: the company's inventory account and profit do not change, only
/// where the stock is.</summary>
public class StockTransfer : AuditableEntity
{
    public string Number { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }

    public Guid FromWarehouseId { get; set; }
    public Warehouse? FromWarehouse { get; set; }
    public Guid ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }

    public StockTransferStatus Status { get; set; } = StockTransferStatus.InTransit;
    public string? Notes { get; set; }

    public string? SentBy { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public string? ReceivedBy { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public string? ReceiveNote { get; set; }

    public List<StockTransferLine> Lines { get; set; } = new();
}

public class StockTransferLine : BaseEntity
{
    public Guid StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    /// <summary>The item's weighted-average cost when it was sent: the transfer carries the same cost to the other branch.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Only for lot-tracked items: the lot that was sent (and its expiry date, so the receiving branch keeps it).</summary>
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
