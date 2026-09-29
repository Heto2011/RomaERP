namespace RomaERP.Domain.Tenancy;

/// <summary>SAR (Gulf) overage prices and shared billing rules, mirroring the public marketing page
/// (marketing/roma-erp.html) — keep both in sync if these change. Per-currency prices for EGP/GBP live in
/// <see cref="SubscriptionPriceList"/>.</summary>
public static class SubscriptionPricingConstants
{
    public const decimal ExtraBranchPrice = 15m;
    public const decimal ExtraUserPrice = 20m;

    /// <summary>Each additional company under the same billing account is charged at 85% of its own computed price.</summary>
    public const decimal AdditionalCompanyDiscountMultiplier = 0.85m;

    /// <summary>Days past the due date an unpaid invoice is tolerated before the tenant is auto-suspended.</summary>
    public const int GracePeriodDays = 7;
}
