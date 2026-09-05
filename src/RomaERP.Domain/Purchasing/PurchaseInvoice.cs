using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Purchasing;

public class PurchaseInvoice : AuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }

    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public PaymentTerm PaymentTerm { get; set; }
    public decimal PaidAmount { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
    public ICollection<PurchasePayment> Payments { get; set; } = new List<PurchasePayment>();
}

public class PurchaseInvoiceLine : BaseEntity
{
    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Which expense/asset account this line is coded to (e.g. rent, admin expenses, inventory).</summary>
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>The purchased inventory Item, when this line represents a stocked item. Null for non-item lines (rent, fees, etc.).</summary>
    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>A cash/card payment recorded against a Credit invoice, reducing its outstanding AP balance.</summary>
public class PurchasePayment : AuditableEntity
{
    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; }
    public string? Reference { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
