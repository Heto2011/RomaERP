using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Purchasing;

public enum PurchaseOrderStatus { Draft = 1, Approved = 2, Converted = 3, Cancelled = 4 }

/// <summary>An order placed with a supplier. It has no accounting effect: nothing is posted until the supplier's invoice is booked from it.</summary>
public class PurchaseOrder : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }

    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string? Notes { get; set; }

    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }

    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }
    /// <summary>Optional here; the invoice needs an account for each line, so it can be chosen when the invoice is made.</summary>
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }
}
