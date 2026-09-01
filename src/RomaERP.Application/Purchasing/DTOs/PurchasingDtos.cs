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
    /// <summary>Optional link to the purchased inventory Item, when this line represents a stocked item.</summary>
    public Guid? ItemId { get; set; }
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

/// <summary>One item physically received into stock — unit cost is VAT-exclusive, same as every other
/// purchase line, so it reflects the item's real cost.</summary>
public class ReceiveInventoryPurchaseLineInputDto
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitCost { get; set; }
}

/// <summary>Records what physically arrived from a vendor, item by item — an internal-control record
/// used by the Restaurant "Purchase Receiving" screen, separate from the vendor's actual invoice (which
/// still gets entered by its total on the accounting Purchase Invoices screen). Deliberately does NOT
/// post to the ledger or touch the vendor's AP balance — that stays the accounting screen's job, so the
/// same delivery is never counted as a liability twice. Only updates item quantity/cost.</summary>
public class ReceiveInventoryPurchaseDto
{
    public Guid VendorId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<ReceiveInventoryPurchaseLineInputDto> Lines { get; set; } = new();
}

public class InventoryReceiptLineDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal NewQuantityOnHand { get; set; }
    public decimal NewAverageCost { get; set; }
}

public class InventoryReceiptDto
{
    public string VendorName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public decimal TotalCost { get; set; }
    public List<InventoryReceiptLineDto> Lines { get; set; } = new();
}

public class PurchaseInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
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

/// <summary>One vendor's outstanding balance broken down by how overdue each invoice's remaining amount is,
/// as of a given date (defaults to today). Only Credit-term invoices with an outstanding balance appear.</summary>
public class VendorAgingDto
{
    public Guid VendorId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Over90Days { get; set; }
}
