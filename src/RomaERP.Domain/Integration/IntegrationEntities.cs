using RomaERP.Domain.Common;

namespace RomaERP.Domain.Integration;

/// <summary>A key an outside program (for example a bookkeeping bot) uses to send data into one company. Only a hash is
/// stored, so the key is shown once when created and can never be read back, only revoked.</summary>
public class IntegrationApiKey : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>SHA-256 of the full key.</summary>
    public string KeyHash { get; set; } = string.Empty;
    /// <summary>First characters of the key, so a person can tell which key is which.</summary>
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
}

public enum IntegrationItemType
{
    Customer = 1,
    Vendor = 2,
    SalesInvoice = 3,
    PurchaseInvoice = 4,
    SalesPayment = 5,
    PurchasePayment = 6,
    Expense = 7,
    JournalEntry = 8,
    /// <summary>Anything else the bot wants to hand over (documents, notes, statements, lists). Stored and reviewed, never posted by itself.</summary>
    Data = 9,
    /// <summary>A stock item (product). Approving creates it, or updates it when the code already exists.</summary>
    Item = 10,
    /// <summary>A line of the client's bank statement. Approving adds it to the bank feed, ready for matching on the reconciliation screen.</summary>
    BankStatementLine = 11
}

public enum IntegrationItemStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Approval was tried and the books refused it (for example an unknown customer); the reason is on the item.</summary>
    Failed = 3
}

/// <summary>One thing the bot sent. It waits here until a person approves it; only then is it turned into the real record.</summary>
public class IntegrationInboxItem : AuditableEntity
{
    public IntegrationItemType Type { get; set; }
    /// <summary>The bot's own id for the item. Sending the same (type, reference) twice never creates two items.</summary>
    public string ExternalRef { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Source { get; set; } = string.Empty;
    public IntegrationItemStatus Status { get; set; } = IntegrationItemStatus.Pending;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>The customer, invoice, journal entry... that approval created.</summary>
    public Guid? ResultEntityId { get; set; }
}
