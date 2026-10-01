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

        // ROMA People (HR only): up to 25 employees, branches unlimited (so no per-branch overage) and a small
        // per-employee overage. Launch offer: the first FoundingCustomerLimit customers pay FoundingBase for their
        // first FoundingInvoiceCount invoices (any country). EGP and GBP are independent prices, like the ERP ones.
        [(PeoplePlanCode, Sar)] = new(Sar, 99, 49.99m, 0, 5),
        [(PeoplePlanCode, Egp)] = new(Egp, 1999, 999, 0, 100),
        [(PeoplePlanCode, Gbp)] = new(Gbp, 32, 16, 0, 1.25m),
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
