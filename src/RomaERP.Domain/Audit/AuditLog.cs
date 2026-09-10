using RomaERP.Domain.Common;

namespace RomaERP.Domain.Audit;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>One recorded change to a tracked entity — who did it, when, and (for updates) the field-level
/// before/after values, so admin-visible actions across the system are traceable after the fact.</summary>
public class AuditLog : BaseEntity
{
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>JSON — for Created/Deleted: {field: value}; for Updated: {field: {old, new}}.</summary>
    public string Changes { get; set; } = "{}";
}
