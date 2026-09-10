using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.Inventory;

public class StockMovement : AuditableEntity
{
    public string MovementNumber { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public StockMovementType MovementType { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Unit cost at the time of this movement (input cost on receipt, weighted-average cost on issue).</summary>
    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public string? Reference { get; set; }
    public string? Description { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
