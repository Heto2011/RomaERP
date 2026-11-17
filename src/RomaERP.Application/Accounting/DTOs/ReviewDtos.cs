namespace RomaERP.Application.Accounting.DTOs;

/// <summary>One check that a balance in the ledger matches the detail behind it.</summary>
public class ReconciliationCheckDto
{
    public string Key { get; set; } = string.Empty;
    public string LedgerLabel { get; set; } = string.Empty;
    public string DetailLabel { get; set; } = string.Empty;
    public decimal LedgerBalance { get; set; }
    public decimal DetailBalance { get; set; }
    public decimal Difference { get; set; }
    public bool Ok { get; set; }
}

/// <summary>One item of the month-end close checklist: fine, or something to deal with first.</summary>
public class CloseCheckDto
{
    public string Key { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public string Link { get; set; } = string.Empty;
}

public class ApprovalLogDto
{
    /// <summary>StockCount, PayrollDebtDeduction, PendingPurchase or CostAllocation.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    /// <summary>Approved or Rejected.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? By { get; set; }
    public DateTime AtUtc { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
}

public class UnusualEntryDto
{
    public Guid EntryId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? Description { get; set; }
    public decimal Total { get; set; }
    /// <summary>Round, Large, Backdated, Reversal, Equity.</summary>
    public List<string> Reasons { get; set; } = new();
}

public class NumberGapDto
{
    public string Series { get; set; } = string.Empty;
    public int Count { get; set; }
    public long First { get; set; }
    public long Last { get; set; }
    public int MissingCount { get; set; }
    /// <summary>The first missing numbers (at most 100).</summary>
    public List<long> Missing { get; set; } = new();
}
