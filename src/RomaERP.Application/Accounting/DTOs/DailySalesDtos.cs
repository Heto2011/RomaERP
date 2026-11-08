namespace RomaERP.Application.Accounting.DTOs;

/// <summary>Daily sales total brought over from another system (till, POS, spreadsheet) as a single posted journal entry.</summary>
public class CreateDailySalesDto
{
    public DateTime SalesDate { get; set; }
    /// <summary>Cash collected, VAT included.</summary>
    public decimal CashAmount { get; set; }
    /// <summary>Card / bank collected, VAT included.</summary>
    public decimal CardAmount { get; set; }
    /// <summary>VAT rate in percent (0 when sales carry no VAT). The entered amounts are treated as VAT-inclusive.</summary>
    public decimal VatRatePercent { get; set; }
    /// <summary>Optional label (branch / source system) so several totals can be entered for the same day.</summary>
    public string? Label { get; set; }
    public string? Notes { get; set; }
}

public class DailySalesEntryDto
{
    public Guid JournalEntryId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime SalesDate { get; set; }
    public string? Label { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal NetSales { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}
