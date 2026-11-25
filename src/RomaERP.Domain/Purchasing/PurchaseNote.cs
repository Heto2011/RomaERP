using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.Purchasing;

/// <summary>Return = goods or money sent back to the supplier (we owe less). AdditionalCharge = the supplier charges more than the invoice (we owe more).</summary>
public enum PurchaseNoteType { Return = 1, AdditionalCharge = 2 }

/// <summary>A correction against a supplier invoice. It moves the supplier balance and the ledger; stock that physically goes back is a separate stock movement.</summary>
public class PurchaseNote : AuditableEntity
{
    public string NoteNumber { get; set; } = string.Empty;
    public PurchaseNoteType NoteType { get; set; }
    public DateTime NoteDate { get; set; }

    public Guid OriginalInvoiceId { get; set; }
    public PurchaseInvoice? OriginalInvoice { get; set; }

    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<PurchaseNoteLine> Lines { get; set; } = new List<PurchaseNoteLine>();
}

public class PurchaseNoteLine : BaseEntity
{
    public Guid PurchaseNoteId { get; set; }
    public PurchaseNote? PurchaseNote { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }

    /// <summary>The account the original purchase was coded to (stock, expense...): a return credits it, an extra charge debits it.</summary>
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
}
