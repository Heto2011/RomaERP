using RomaERP.Domain.Common;

namespace RomaERP.Domain.Assistant;

/// <summary>One row per (feature, calendar day) — a running count of real Claude API calls made today.
/// Enforces a daily cap per tenant so a bug or runaway usage can't rack up unbounded API cost. Lives in the
/// tenant's own database (database-per-tenant), so the cap is naturally scoped per company with no tenant id
/// needed here.</summary>
public class AiUsageCounter : BaseEntity
{
    public string FeatureKey { get; set; } = string.Empty;
    public DateTime UsageDate { get; set; }
    public int Count { get; set; }
}
