using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Sales;

public class SalesInvoice : AuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    /// <summary>Warehouse stock is issued from — required only when at least one line is linked to an inventory Item.</summary>
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Chosen once at invoice creation: Cash/Card settle it immediately (no AR involved);
    /// Credit posts it to the customer's Accounts Receivable balance for later collection.</summary>
    public PaymentTerm PaymentTerm { get; set; }
    public decimal PaidAmount { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public string? Notes { get; set; }

    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
    public ICollection<SalesPayment> Payments { get; set; } = new List<SalesPayment>();
}

public class SalesInvoiceLine : BaseEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>Set when this line is fulfilled from inventory — triggers a stock issue and COGS posting.
    /// Left null for free-text/service lines that don't touch inventory.</summary>
    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }
}

/// <summary>A cash/card collection recorded against a Credit invoice, reducing its outstanding AR balance.</summary>
public class SalesPayment : AuditableEntity
{
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; }
    public string? Reference { get; set; }

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
