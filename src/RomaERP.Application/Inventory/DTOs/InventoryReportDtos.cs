using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.DTOs;

public class StockValuationLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCost { get; set; }
    public decimal Value { get; set; }
}

/// <summary>QuantityOnHand is a live running total (RomaERP has no per-warehouse stock split, no daily
/// balance history) — this is always "as of right now," not a historical point in time.</summary>
public class StockValuationReportDto
{
    public DateTime AsOfDate { get; set; }
    public List<StockValuationLineDto> Items { get; set; } = new();
    public decimal TotalValue { get; set; }
}

public class InventoryMovementLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal StockValue { get; set; }
    public decimal QuantityIssuedInPeriod { get; set; }
    public decimal CogsInPeriod { get; set; }
    /// <summary>Null when nothing sold in the period — usage rate is zero, so "days remaining" is undefined
    /// rather than infinite.</summary>
    public decimal? DaysOfStockRemaining { get; set; }
    /// <summary>CogsInPeriod / current StockValue — a period ratio, not annualized.</summary>
    public decimal TurnoverRate { get; set; }
    public bool IsAtRiskOfStockout { get; set; }
    public bool IsDeadStock { get; set; }
    public bool IsExcessStock { get; set; }
}

/// <summary>Feeds Slow/Fast Moving, Dead Stock, Excess Stock, Days of Inventory, Turnover Rate, and
/// At-Risk-of-Stockout from one dataset. QuantityIssuedInPeriod/CogsInPeriod come from real StockMovement
/// Issue rows (the actual weighted-average cost at the moment of each movement) — this covers direct item
/// sales, recipe-ingredient consumption, and any manual stock issues alike, not sales alone. StockValue/
/// TurnoverRate use the *current* QuantityOnHand snapshot as the inventory-value baseline — RomaERP has no
/// daily inventory-balance history to compute a true period-average value from.</summary>
public class InventoryMovementReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<InventoryMovementLineDto> Items { get; set; } = new();
}

public class PurchasePriceVarianceLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime PreviousReceiptDate { get; set; }
    public decimal PreviousUnitCost { get; set; }
    public DateTime LatestReceiptDate { get; set; }
    public decimal LatestUnitCost { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal ChangePercent { get; set; }
}

/// <summary>Compares each item's most recent stock-receipt cost (within the period) against its prior receipt
/// cost, using StockMovement Receipt rows — the actual cost applied to stock at each receipt. Items with
/// fewer than two receipts ever are excluded — there's nothing real to compare.</summary>
public class PurchasePriceVarianceReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<PurchasePriceVarianceLineDto> Items { get; set; } = new();
}

public class RecipeCostLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public bool HasRecipe { get; set; }
    public decimal RecipeCost { get; set; }
    public decimal MenuPrice { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

/// <summary>Every menu item's real unit cost: built from its MenuRecipeLine bill of materials priced at each
/// raw material's current AverageCost, falling back to the item's own AverageCost when it has no recipe lines
/// (sold as its own raw material). Same computation already used by Sales Channel Profitability, exposed here
/// per-item instead of aggregated by channel.</summary>
public class RecipeCostReportDto
{
    public List<RecipeCostLineDto> Items { get; set; } = new();
}

public class WasteByItemLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public int EntryCount { get; set; }
}

public class WasteByReasonLineDto
{
    public WasteReason Reason { get; set; }
    public decimal TotalCost { get; set; }
    public decimal PercentOfTotal { get; set; }
}

public class WasteTrendPointDto
{
    public DateTime WeekStart { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>Aggregates real WasteEntry records (each one a genuine GL-posted stock issue, not an estimate) into
/// a trend, a per-item breakdown, and a reason breakdown for the period. WasteCostPercentOfCogs compares total
/// waste cost against total COGS issued in the same period (from real StockMovement Issue rows) — the closest
/// thing to a "waste as % of usage" figure RomaERP's data actually supports; it is not a percentage of sales
/// revenue, since waste spans channels (POS, direct sales, manual issues) that aren't all revenue-bearing.</summary>
public class WasteAnalysisReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalWasteCost { get; set; }
    public decimal TotalWasteQuantity { get; set; }
    public decimal CogsInPeriod { get; set; }
    public decimal? WasteCostPercentOfCogs { get; set; }
    public List<WasteByItemLineDto> TopWastedItems { get; set; } = new();
    public List<WasteByReasonLineDto> ByReason { get; set; } = new();
    public List<WasteTrendPointDto> WeeklyTrend { get; set; } = new();
}
