using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Sales;

public enum SalesOrderStatus { Draft = 1, Confirmed = 2, Delivered = 3, Invoiced = 4, Cancelled = 5 }

/// <summary>A customer's confirmed order. It has no accounting and does not move stock: both happen when the order is invoiced (the invoice issues the stock). "Delivered" only records that the goods went out, and a delivery note can be printed from it.</summary>
public class SalesOrder : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }

    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public Guid? QuotationId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string? DeliveredBy { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}

public class SalesOrderLine : BaseEntity
{
    public Guid SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

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
