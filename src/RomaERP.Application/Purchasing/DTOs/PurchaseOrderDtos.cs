using RomaERP.Domain.Common;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Application.Purchasing.DTOs;

public class PurchaseOrderLineInputDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? AccountId { get; set; }
}

public class SavePurchaseOrderDto
{
    public Guid VendorId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineInputDto> Lines { get; set; } = new();
}

public class ConvertPurchaseOrderDto
{
    public DateTime InvoiceDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    /// <summary>Used for any line that has no account of its own.</summary>
    public Guid? DefaultAccountId { get; set; }
    public Guid? ReceiveToWarehouseId { get; set; }
}

public class PurchaseOrderLineDto
{
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? AccountId { get; set; }
}

public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}
