namespace RomaERP.Application.Accounting.DTOs;

public class CostAllocationAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    /// <summary>What sits on head office for this account in the month and has not been moved to the branches yet.</summary>
    public decimal Amount { get; set; }
}

public class CostAllocationBranchDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    /// <summary>The branch's share of the month's sales, the proposed basis (percent, two decimals).</summary>
    public decimal SharePercent { get; set; }
}

/// <summary>What the month-end allocation of shared costs would do, proposed on each branch's share of sales.</summary>
public class CostAllocationPreviewDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CostAllocationAccountDto> Accounts { get; set; } = new();
    public decimal TotalToAllocate { get; set; }
    public List<CostAllocationBranchDto> Branches { get; set; } = new();
    /// <summary>Why nothing can be allocated yet (e.g. no branch sales in the month); empty when it can.</summary>
    public string? Blocked { get; set; }
}

public class ApplyCostAllocationDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>Percent per branch; must total 100. Left out, the proposed sales shares are used.</summary>
    public List<BranchShareDto> Shares { get; set; } = new();
    public string? Note { get; set; }
}

public class BranchShareDto
{
    public Guid WarehouseId { get; set; }
    public decimal SharePercent { get; set; }
}
