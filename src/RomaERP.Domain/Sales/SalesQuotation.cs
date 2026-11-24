using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Sales;

public enum QuotationStatus { Draft = 1, Sent = 2, Accepted = 3, Rejected = 4, Converted = 5 }

/// <summary>An offer to a customer. It has no accounting effect: nothing is posted until it is turned into a sales invoice.</summary>
public class SalesQuotation : AuditableEntity
{
    public string QuotationNumber { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; }
    public DateTime? ValidUntil { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public string? Notes { get; set; }
    /// <summary>Printed under the lines: delivery, payment and validity terms.</summary>
    public string? Terms { get; set; }

    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid? ConvertedInvoiceId { get; set; }

    public ICollection<SalesQuotationLine> Lines { get; set; } = new List<SalesQuotationLine>();
}

public class SalesQuotationLine : BaseEntity
{
    public Guid SalesQuotationId { get; set; }
    public SalesQuotation? SalesQuotation { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }

    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }
}
