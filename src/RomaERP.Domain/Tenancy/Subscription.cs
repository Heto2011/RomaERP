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

/// <summary>One tenant's billing relationship: which plan, current billing period, and how it pays.
/// <see cref="PaymentProvider"/> starts as "Manual" (admin records bank-transfer payments by hand) and can be
/// switched to a real gateway (e.g. "Moyasar") once a saved card token exists for auto-charge.</summary>
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

    public string PaymentProvider { get; set; } = "Manual";
    public string? PaymentProviderCustomerRef { get; set; }
    public string? PaymentProviderTokenRef { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }
}
