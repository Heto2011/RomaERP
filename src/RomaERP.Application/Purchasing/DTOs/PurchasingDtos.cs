using RomaERP.Domain.Common;

namespace RomaERP.Application.Purchasing.DTOs;

public class VendorDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public bool IsActive { get; set; }
    public decimal ApBalance { get; set; }
}

public class CreateVendorDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxRegistrationNumber { get; set; }
}

public class PurchaseInvoiceLineInputDto
{
    public string Description { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class CreatePurchaseInvoiceDto
{
    public Guid VendorId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseInvoiceLineInputDto> Lines { get; set; } = new();
}

public class PurchaseInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchasePaymentDto
{
    public Guid Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; }
    public string? Reference { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public class RecordPurchasePaymentDto
{
    public decimal Amount { get; set; }
    public PaymentTerm Method { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Reference { get; set; }
}

public class PurchaseInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseInvoiceLineDto> Lines { get; set; } = new();
    public List<PurchasePaymentDto> Payments { get; set; } = new();
}
