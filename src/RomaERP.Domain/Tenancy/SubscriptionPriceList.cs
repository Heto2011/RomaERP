namespace RomaERP.Domain.Tenancy;

/// <summary>The price a plan bills in one currency: the monthly base plus that currency's own per-extra-branch /
/// per-extra-user overage. EGP and GBP are independent price lists, not conversions of the SAR one.</summary>
public record PlanPriceSet(string Currency, decimal Base, decimal FoundingBase, decimal ExtraBranch, decimal ExtraUser);

/// <summary>Mirrors the tier prices on the public marketing page (marketing/roma-erp.html: TIERS[] and
/// INDEPENDENT_PRICING) — keep both in sync if either changes. <c>FoundingBase</c> is the launch price
/// (zero = no founding offer for that tier/currency); see <see cref="FoundingCustomerLimit"/>.</summary>
public static class SubscriptionPriceList
{
    /// <summary>Egypt founding offer: first 15 customers, first 3 monthly invoices at <c>FoundingBase</c>.</summary>
    public const int FoundingCustomerLimit = 15;
    public const int FoundingInvoiceCount = 3;

    public const string Sar = "SAR";
    public const string Egp = "EGP";
    public const string Gbp = "GBP";

    private static readonly Dictionary<(string Plan, string Currency), PlanPriceSet> Prices = new()
    {
        [("essential", Sar)] = new(Sar, 149, 0, 15, 20),
        [("business", Sar)] = new(Sar, 349, 0, 15, 20),
        [("professional", Sar)] = new(Sar, 649, 0, 15, 20),
        [("enterprise", Sar)] = new(Sar, 2499, 0, 15, 20),

        // Egypt: Base is the regular list price, FoundingBase the 50%-off launch price.
        [("essential", Egp)] = new(Egp, 3000, 1500, 200, 400),
        [("business", Egp)] = new(Egp, 7000, 3500, 200, 400),
        [("professional", Egp)] = new(Egp, 12000, 6000, 200, 400),
        [("enterprise", Egp)] = new(Egp, 35000, 0, 200, 400),

        [("essential", Gbp)] = new(Gbp, 49, 0, 5, 3),
        [("business", Gbp)] = new(Gbp, 99, 0, 5, 3),
        [("professional", Gbp)] = new(Gbp, 179, 0, 5, 3),
        [("enterprise", Gbp)] = new(Gbp, 599, 0, 5, 3),
    };

    /// <summary>Egypt bills in EGP and the UK in GBP (each its own independent list); every other country
    /// (Saudi Arabia and the rest of the Gulf) is billed on the SAR list.</summary>
    public static string CurrencyFor(Country country) => country switch
    {
        Country.Egypt => Egp,
        Country.UnitedKingdom => Gbp,
        _ => Sar,
    };

    public static PlanPriceSet? Find(string planCode, string currency)
        => Prices.GetValueOrDefault((planCode.ToLowerInvariant(), currency.ToUpperInvariant()));
}
