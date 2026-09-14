namespace RomaERP.Domain.Tenancy;

/// <summary>Mirrors the overage pricing shown on the public marketing page (marketing/pricing.html) — keep
/// both in sync if these change.</summary>
public static class SubscriptionPricingConstants
{
    public const decimal ExtraBranchPrice = 150m;
    public const decimal ExtraUserPrice = 40m;

    /// <summary>Each additional company under the same billing account is charged at 85% of its own computed price.</summary>
    public const decimal AdditionalCompanyDiscountMultiplier = 0.85m;

    public const string DefaultCurrency = "SAR";

    /// <summary>Days past the due date an unpaid invoice is tolerated before the tenant is auto-suspended.</summary>
    public const int GracePeriodDays = 7;
}
