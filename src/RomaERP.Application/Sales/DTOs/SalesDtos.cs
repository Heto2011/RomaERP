using RomaERP.Domain.Common;

namespace RomaERP.Application.Sales.DTOs;

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public bool IsActive { get; set; }
    public decimal ArBalance { get; set; }
}

public class CreateCustomerDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
}

public class SalesInvoiceLineInputDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }

    /// <summary>Optional — set to fulfill this line from inventory (issues stock and posts COGS).</summary>
    public Guid? ItemId { get; set; }
}

public class CreateSalesInvoiceDto
{
    public Guid CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    public string? Notes { get; set; }

    /// <summary>Required only when at least one line has an ItemId.</summary>
    public Guid? WarehouseId { get; set; }
    public List<SalesInvoiceLineInputDto> Lines { get; set; } = new();
}

public class SalesInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
}

public class SalesPaymentDto
{
    public Guid Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; }
    public string? Reference { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public class RecordSalesPaymentDto
{
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Reference { get; set; }
}

public class SalesInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public List<SalesInvoiceLineDto> Lines { get; set; } = new();
    public List<SalesPaymentDto> Payments { get; set; } = new();
    public RomaERP.Domain.EInvoicing.EInvoiceStatus EInvoiceStatus { get; set; }
    public string? EInvoiceExternalUuid { get; set; }
    public DateTime? EInvoiceSubmittedAtUtc { get; set; }
    public string? EInvoiceErrorMessage { get; set; }
}

/// <summary>One customer's outstanding balance broken down by how overdue each invoice's remaining amount is,
/// as of a given date (defaults to today). Only Credit-term invoices with an outstanding balance appear.</summary>
public class CustomerAgingDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Over90Days { get; set; }
}
