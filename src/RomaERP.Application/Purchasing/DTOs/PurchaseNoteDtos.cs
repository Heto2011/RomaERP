using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Purchasing.DTOs;

public class PurchaseNoteLineInputDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    /// <summary>Tax rate as a fraction (0.14 = 14%, 0 = untaxed). Empty = the company rate.</summary>
    public decimal? VatRate { get; set; }
    public Guid AccountId { get; set; }
}

public class CreatePurchaseNoteDto
{
    public Guid OriginalInvoiceId { get; set; }
    public PurchaseNoteType NoteType { get; set; }
    public DateTime NoteDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<PurchaseNoteLineInputDto> Lines { get; set; } = new();
}

public class PurchaseNoteLineDto
{
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public Guid AccountId { get; set; }
}

public class PurchaseNoteDto
{
    public Guid Id { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public PurchaseNoteType NoteType { get; set; }
    public DateTime NoteDate { get; set; }
    public Guid OriginalInvoiceId { get; set; }
    public string OriginalInvoiceNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public List<PurchaseNoteLineDto> Lines { get; set; } = new();
}
