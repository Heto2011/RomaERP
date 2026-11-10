using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>One line of a company's prepaid AI-credit balance: +N when credits are granted or bought, -1 each time a call uses
/// one. Kept in the central database (not the company's own) so a company can never edit its own balance. The balance is the
/// sum of every entry.</summary>
public class TenantCreditEntry : BaseEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Number of AI calls added (positive) or used (negative).</summary>
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
