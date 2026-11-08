namespace RomaERP.Application.Purchasing.DTOs;

public class PendingPurchaseInvoiceDto
{
    public Guid Id { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public int LineCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DecidedByName { get; set; }
    public string? RejectionReason { get; set; }
}

public class RejectPendingPurchaseDto
{
    public string? Reason { get; set; }
}
