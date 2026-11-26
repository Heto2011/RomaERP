using RomaERP.Domain.Common;

namespace RomaERP.Domain.Sales;

/// <summary>A private link a customer opens to see their own invoices and statement, with no sign-in. Only a hash of the secret is stored, so the link can be shown once and never read back; making a new one switches the old one off.</summary>
public class CustomerPortalLink : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>SHA-256 (hex) of the secret in the link.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastViewedAtUtc { get; set; }
    public int ViewCount { get; set; }
}
