using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.DTOs;

public class QuotationLineInputDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    /// <summary>Tax rate for the line as a fraction (0.14 = 14%, 0 = untaxed). Empty = the company rate.</summary>
    public decimal? VatRate { get; set; }
    public Guid? ItemId { get; set; }
}

public class SaveQuotationDto
{
    public Guid CustomerId { get; set; }
    public DateTime QuotationDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public List<QuotationLineInputDto> Lines { get; set; } = new();
}

public class SetQuotationStatusDto { public QuotationStatus Status { get; set; } }

public class ConvertQuotationDto
{
    public DateTime InvoiceDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    /// <summary>Required only when a line is linked to a stock item.</summary>
    public Guid? WarehouseId { get; set; }
}

public class QuotationLineDto
{
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public Guid? ItemId { get; set; }
}

public class QuotationDto
{
    public Guid Id { get; set; }
    public string QuotationNumber { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsExpired { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public QuotationStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public decimal SubTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public List<QuotationLineDto> Lines { get; set; } = new();
}
