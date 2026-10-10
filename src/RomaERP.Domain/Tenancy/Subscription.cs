using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

public enum SubscriptionStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Cancelled = 4,
}

public enum BillingPeriod
{
    Monthly = 0,
    /// <summary>Paid once for 12 months, charged as <see cref="SubscriptionPriceList.AnnualMonthsCharged"/> months.</summary>
    Annual = 1,
}

/// <summary>One tenant's billing relationship: which plan, current billing period, and how it pays.
/// <see cref="PaymentProvider"/> starts as "Manual" (admin records bank-transfer payments by hand) and can be
/// switched to a real gateway (e.g. "PayTabs") once a saved card token exists for auto-charge.</summary>
public class Subscription : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>Tenants sharing this id are the same customer's other companies — each invoice after the
    /// first one due in a billing run is discounted per <see cref="SubscriptionPricingConstants.AdditionalCompanyDiscountMultiplier"/>.</summary>
    public Guid? BillingAccountId { get; set; }

    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    public string PaymentProvider { get; set; } = "Manual";
    public string? PaymentProviderCustomerRef { get; set; }
    public string? PaymentProviderTokenRef { get; set; }

    /// <summary>Extra branches / users this company pays for on top of its plan (card payment through Lemon Squeezy:
    /// each is its own small monthly subscription whose quantity is the number of extras).</summary>
    public int ExtraBranchesPaid { get; set; }
    public int ExtraUsersPaid { get; set; }
    public string? ExtraBranchesLemonSubscriptionId { get; set; }
    public string? ExtraUsersLemonSubscriptionId { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }
}
