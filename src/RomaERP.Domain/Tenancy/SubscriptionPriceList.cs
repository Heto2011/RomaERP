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

    /// <summary>Plan code of the standalone ROMA People (HR) product — mirrors marketing/roma-hr.html.</summary>
    public const string PeoplePlanCode = "people";
    public const int PeoplePlanIncludedEmployees = 25;

    /// <summary>Egypt-only entry plan: one branch, five users, no extras (more means moving up to Essential).</summary>
    public const string MiniPlanCode = "mini";
    public const int MiniPlanIncludedBranches = 1;
    public const int MiniPlanIncludedUsers = 5;

    /// <summary>Egypt: how many paid extras a plan allows on top of what it includes. Mini allows none; every other
    /// plan allows 40% of its included branches/users (the same rule the public price calculator uses), e.g. Essential
    /// allows +1 branch and +4 users. Null = no cap (not Egypt, or an uncapped plan such as Enterprise or Roma HR).</summary>
    public static (int Branches, int Users)? EgyptExtrasCap(string planCode, int includedBranches, int includedUsers)
    {
        if (string.Equals(planCode, MiniPlanCode, StringComparison.OrdinalIgnoreCase)) return (0, 0);
        if (includedBranches == int.MaxValue || includedUsers == int.MaxValue) return null;
        if (planCode.ToLowerInvariant() is not ("essential" or "business" or "professional")) return null;
        return ((int)Math.Round(includedBranches * 0.4, MidpointRounding.AwayFromZero), (int)Math.Round(includedUsers * 0.4, MidpointRounding.AwayFromZero));
    }

    /// <summary>Paying a year up front is charged as 10 of the 12 months — "two months free".</summary>
    public const int AnnualMonthsCharged = 10;
    public const int AnnualMonthsCovered = 12;

    /// <summary>Enterprise has no published price and no automatic discounts (annual "pay 10 get 12", Egypt founding price):
    /// it is quoted and invoiced by agreement when a real customer of that size appears.</summary>
    private static readonly HashSet<string> NoAutomaticDiscountPlans = new(StringComparer.OrdinalIgnoreCase) { "enterprise" };

    public static bool SupportsAnnual(string planCode) => !NoAutomaticDiscountPlans.Contains(planCode);

    public const string Sar = "SAR";
    public const string Egp = "EGP";
    public const string Gbp = "GBP";

    /// <summary>The other Gulf currencies are pegged to the USD, so (exactly as on the public page) each is the SAR
    /// list at a fixed rate, rounded to that currency's displayed decimals: units of currency per 1 SAR.</summary>
    private static readonly (string Currency, decimal Rate, int Decimals)[] GulfCurrencies =
    {
        ("AED", 0.98m, 0), ("QAR", 0.97m, 0), ("KWD", 0.082m, 1), ("BHD", 0.1m, 1), ("OMR", 0.1025m, 1),
    };

    private static readonly Dictionary<(string Plan, string Currency), PlanPriceSet> Prices = new()
    {
        [("essential", Sar)] = new(Sar, 149, 0, 15, 20),
        [("business", Sar)] = new(Sar, 349, 0, 15, 20),
        [("professional", Sar)] = new(Sar, 649, 0, 15, 20),
        // Enterprise: no published price (0) — quoted per customer, see NoAutomaticDiscountPlans.
        [("enterprise", Sar)] = new(Sar, 0, 0, 0, 0),

        // Egypt: its own price list (not a conversion). No launch discount on the ERP plans — the Mini plan is the entry price.
        [(MiniPlanCode, Egp)] = new(Egp, 499, 0, 0, 0),
        [("essential", Egp)] = new(Egp, 999, 0, 200, 100),
        [("business", Egp)] = new(Egp, 2499, 0, 200, 100),
        [("professional", Egp)] = new(Egp, 4499, 0, 200, 100),
        [("enterprise", Egp)] = new(Egp, 0, 0, 0, 0),

        [("essential", Gbp)] = new(Gbp, 49, 0, 5, 3),
        [("business", Gbp)] = new(Gbp, 99, 0, 5, 3),
        [("professional", Gbp)] = new(Gbp, 179, 0, 5, 3),
        [("enterprise", Gbp)] = new(Gbp, 0, 0, 0, 0),

        // ROMA People (HR only): up to 25 employees, branches unlimited (so no per-branch overage) and a small
        // per-employee overage. Launch offer: the first FoundingCustomerLimit customers pay FoundingBase for their
        // first FoundingInvoiceCount invoices (any country). EGP and GBP are independent prices, like the ERP ones.
        [(PeoplePlanCode, Sar)] = new(Sar, 99, 49.99m, 0, 5),
        [(PeoplePlanCode, Egp)] = new(Egp, 799, 400, 0, 100),
        [(PeoplePlanCode, Gbp)] = new(Gbp, 49, 24.5m, 0, 1.25m),
    };

    static SubscriptionPriceList()
    {
        foreach (var (currency, rate, decimals) in GulfCurrencies)
        {
            foreach (var plan in new[] { "essential", "business", "professional", "enterprise", PeoplePlanCode })
            {
                var sar = Prices[(plan, Sar)];
                Prices[(plan, currency)] = new PlanPriceSet(currency,
                    Math.Round(sar.Base * rate, decimals, MidpointRounding.AwayFromZero),
                    Math.Round(sar.FoundingBase * rate, decimals, MidpointRounding.AwayFromZero),
                    sar.ExtraBranch * rate, sar.ExtraUser * rate);
            }
        }
    }

    /// <summary>Every country is billed in its own currency (Guernsey and the UK both in sterling).</summary>
    public static string CurrencyFor(Country country) => country switch
    {
        Country.Egypt => Egp,
        Country.SaudiArabia => Sar,
        Country.UAE => "AED",
        Country.Bahrain => "BHD",
        Country.Oman => "OMR",
        Country.Qatar => "QAR",
        Country.Kuwait => "KWD",
        Country.UnitedKingdom or Country.Guernsey => Gbp,
        _ => Sar,
    };

    /// <summary>How many decimals an invoice amount in this currency is rounded to (matches the public page).</summary>
    public static int DecimalsFor(string currency)
        => currency.ToUpperInvariant() is Sar or Gbp ? 2 : GulfCurrencies.FirstOrDefault(c => c.Currency == currency.ToUpperInvariant()) is { Currency: not null } g ? g.Decimals : 0;

    public static PlanPriceSet? Find(string planCode, string currency)
        => Prices.GetValueOrDefault((planCode.ToLowerInvariant(), currency.ToUpperInvariant()));
}
