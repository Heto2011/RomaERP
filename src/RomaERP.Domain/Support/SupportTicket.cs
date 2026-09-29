using RomaERP.Domain.Common;

namespace RomaERP.Domain.Support;

public enum SupportTicketStatus
{
    Open = 1,
    AwaitingCustomer = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5
}

public enum SupportMessageSender
{
    Customer = 1,
    Support = 2,
    Ai = 3
}

/// <summary>Lives in the CENTRAL database, not a tenant's — support is one unified inbox across every
/// tenant, unlike almost everything else in the system which is fully isolated per tenant.</summary>
public class SupportTicket : AuditableEntity
{
    /// <summary>Short, human-friendly sequential number (starts at 1000) shown to the customer instead of
    /// the internal Guid — assigned by the database as an identity column.</summary>
    public int TicketNumber { get; set; }

    public Guid TenantId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public List<SupportTicketMessage> Messages { get; set; } = new();
}

public class SupportTicketMessage : AuditableEntity
{
    public Guid TicketId { get; set; }
    public SupportMessageSender SenderType { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public List<SupportTicketAttachment> Attachments { get; set; } = new();
}

/// <summary>Stored inline as bytes rather than on disk/blob storage — expected volume (small text-support
/// attachments, capped at 5MB each) doesn't justify a separate file store yet.</summary>
public class SupportTicketAttachment : AuditableEntity
{
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
