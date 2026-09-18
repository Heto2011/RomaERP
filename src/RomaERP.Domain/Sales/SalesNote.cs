using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.EInvoicing;

namespace RomaERP.Domain.Sales;

/// <summary>Credit note (يقلل مديونية العميل — إرجاع بضاعة/خصم) or Debit note (يزود مديونية العميل — رسوم
/// إضافية/تصحيح لأقل). Modeled as one entity with a Type discriminator, mirroring how ZATCA itself represents
/// both as the same UBL Invoice document distinguished only by InvoiceTypeCode (381 vs 383).</summary>
public enum SalesNoteType
{
    Credit = 1,
    Debit = 2
}

public class SalesNote : AuditableEntity
{
    public string NoteNumber { get; set; } = string.Empty;
    public SalesNoteType NoteType { get; set; }
    public DateTime NoteDate { get; set; }

    public Guid OriginalInvoiceId { get; set; }
    public SalesInvoice? OriginalInvoice { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    /// <summary>Required — why the note was issued (return, pricing error, damaged goods, extra charge...).
    /// ZATCA and most tax authorities require a stated reason for credit/debit notes.</summary>
    public string Reason { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public string? Notes { get; set; }

    // ----- E-invoicing (government tax authority integration) -----
    public EInvoiceStatus EInvoiceStatus { get; set; } = EInvoiceStatus.NotSubmitted;
    public string? EInvoiceExternalUuid { get; set; }
    public string? EInvoiceHash { get; set; }
    public DateTime? EInvoiceSubmittedAtUtc { get; set; }
    public string? EInvoiceErrorMessage { get; set; }

    public ICollection<SalesNoteLine> Lines { get; set; } = new List<SalesNoteLine>();
}

public class SalesNoteLine : BaseEntity
{
    public Guid SalesNoteId { get; set; }
    public SalesNote? SalesNote { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
