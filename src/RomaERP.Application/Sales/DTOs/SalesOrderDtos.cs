using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.DTOs;

public class SaveSalesOrderDto
{
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }
    public Guid? QuotationId { get; set; }
    public List<QuotationLineInputDto> Lines { get; set; } = new();
}

public class ConvertSalesOrderDto
{
    public DateTime InvoiceDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    /// <summary>Required only when a line is linked to a stock item (the invoice issues that stock).</summary>
    public Guid? WarehouseId { get; set; }
}

public class SalesOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public SalesOrderStatus Status { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? QuotationId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? DeliveredBy { get; set; }
    public List<QuotationLineDto> Lines { get; set; } = new();
}
