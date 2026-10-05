using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>One line in a company's activity trail, shown to the platform owner in the system console: password changes
/// and resets, subscription changes (plan, payment, suspension, reactivation), user management, trial expiry, and so on.
/// Lives in the central database so it is readable across companies and survives anything done inside a tenant.</summary>
public class TenantActivity : BaseEntity
{
    public Guid TenantId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Password, Subscription, Billing, Status, Users, Company.</summary>
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }

    /// <summary>Who did it: an email, "system console", or "automatic".</summary>
    public string? Actor { get; set; }
}
