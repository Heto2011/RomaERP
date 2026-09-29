using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>Permanent proof that a tenant's data-deletion request was received and honored — lives in the
/// central database (never the tenant's own, since that database is what gets dropped) and is never
/// deleted itself, so the business always has a durable record of who asked, when, and who processed it,
/// even years after the tenant's own database and every row about them is gone.</summary>
public class DataDeletionRecord : AuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Snapshot of identifying details at the time of deletion — the Tenant row (and everything
    /// about the company) may no longer exist afterward, so this record has to stand on its own.</summary>
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;

    /// <summary>A short, quotable reference (e.g. "DEL-1000") a customer or regulator can cite later.</summary>
    public int ConfirmationNumber { get; set; }

    public string RequestedByEmail { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    public string ProcessedByEmail { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureReason { get; set; }
}
