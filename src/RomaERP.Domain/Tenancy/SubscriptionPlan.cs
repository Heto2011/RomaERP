using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>A billable tier in the central database, matching the public pricing page (Essential/Business/
/// Professional/Enterprise). Price is per month; branches/users beyond what's included are billed as overage
/// at <see cref="SubscriptionPricingConstants"/> rates.</summary>
public class SubscriptionPlan : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal MonthlyBasePrice { get; set; }
    public int IncludedBranches { get; set; }
    public int IncludedUsers { get; set; }

    /// <summary>Enterprise: price is negotiated, so overage is never auto-computed for it.</summary>
    public bool IsCustomPricing { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
