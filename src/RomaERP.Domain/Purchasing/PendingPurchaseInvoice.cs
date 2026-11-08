using RomaERP.Domain.Common;

namespace RomaERP.Domain.Purchasing;

public enum PendingPurchaseStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>A purchase invoice entered by someone who may record purchases but not approve them. It holds the exact
/// request as JSON and posts nothing (no journal entry, no vendor balance, no stock) until an Admin or Accountant approves it.</summary>
public class PendingPurchaseInvoice : AuditableEntity
{
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;

    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public int LineCount { get; set; }

    /// <summary>The CreatePurchaseInvoiceDto (plus the optional stock warehouse), serialized as JSON.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public PendingPurchaseStatus Status { get; set; } = PendingPurchaseStatus.Pending;
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ApprovedInvoiceId { get; set; }
}
