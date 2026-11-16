namespace RomaERP.Application.Inventory.DTOs;

public class BranchMinimumDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal MinimumQuantity { get; set; }
    public decimal Quantity { get; set; }
}

public class SetBranchMinimumDto
{
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    /// <summary>Zero or empty removes the minimum.</summary>
    public decimal MinimumQuantity { get; set; }
}

public class TransferSuggestionDto
{
    public Guid FromWarehouseId { get; set; }
    public string FromWarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

/// <summary>A branch below the minimum of an item, with where the shortage could be covered from.</summary>
public class BranchShortageAlertDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal MinimumQuantity { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Already on its way to the branch (sent, not yet received).</summary>
    public decimal Incoming { get; set; }
    public decimal Shortage { get; set; }
    public List<TransferSuggestionDto> Suggestions { get; set; } = new();
    /// <summary>What no other branch can cover: this much has to be bought.</summary>
    public decimal ToBuy { get; set; }
}
