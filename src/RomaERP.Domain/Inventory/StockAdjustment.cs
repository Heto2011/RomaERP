using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

public enum StockAdjustmentReason
{
    /// <summary>Stock the branch already had when the books started (or that was never recorded): added against the company's equity, not a supplier.</summary>
    OpeningStock = 1,
    /// <summary>More found in a count than the books show.</summary>
    CountGain = 2,
    /// <summary>Less found in a count than the books show.</summary>
    CountLoss = 3,
    Other = 4
}

/// <summary>A correction of one item's stock in one branch, with a reason: up when the stock is there but not in the books (no supplier
/// involved), down when it is missing. It posts its own journal entry.</summary>
public class StockAdjustment : AuditableEntity
{
    public string Number { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public bool IsIncrease { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public StockAdjustmentReason Reason { get; set; }
    public string? Notes { get; set; }
    public string? LotNumber { get; set; }
    public string? CreatedByName { get; set; }

    public Guid StockMovementId { get; set; }
    public Guid? JournalEntryId { get; set; }
}
