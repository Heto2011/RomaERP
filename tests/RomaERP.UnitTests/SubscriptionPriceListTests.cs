using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Pins the billing price list to the numbers published on marketing/roma-erp.html — if the page changes,
/// this fails until SubscriptionPriceList is updated to match.</summary>
public class SubscriptionPriceListTests
{
    [Theory]
    [InlineData("essential", "SAR", 149)]
    [InlineData("business", "SAR", 349)]
    [InlineData("professional", "SAR", 649)]
    [InlineData("mini", "EGP", 499)]
    [InlineData("essential", "EGP", 999)]
    [InlineData("business", "EGP", 2499)]
    [InlineData("professional", "EGP", 4499)]
    [InlineData("essential", "GBP", 49)]
    [InlineData("business", "GBP", 99)]
    [InlineData("professional", "GBP", 179)]
    public void PublishedBasePrices(string plan, string currency, int expected)
        => Assert.Equal(expected, SubscriptionPriceList.Find(plan, currency)!.Base);

    [Fact]
    public void EgyptErpPlansHaveNoLaunchDiscount_ButRomaHrKeepsIt()
    {
        foreach (var plan in new[] { "mini", "essential", "business", "professional" })
            Assert.Equal(0, SubscriptionPriceList.Find(plan, "EGP")!.FoundingBase);
        var hr = SubscriptionPriceList.Find(SubscriptionPriceList.PeoplePlanCode, "EGP")!;
        Assert.Equal(799, hr.Base);
        Assert.Equal(400, hr.FoundingBase);
    }

    [Theory]
    [InlineData("mini", 1, 5, 0, 0)]                      // Mini: nothing beyond what it includes
    [InlineData("essential", 3, 10, 1, 4)]                // 40% of what it includes: +1 branch, +4 users (14 users in total)
    [InlineData("business", 7, 25, 3, 10)]
    [InlineData("professional", 15, 50, 6, 20)]
    public void EgyptExtrasCap_IsZeroForMini_AndFortyPercentForTheOthers(string plan, int branches, int users, int maxBranches, int maxUsers)
        => Assert.Equal((maxBranches, maxUsers), SubscriptionPriceList.EgyptExtrasCap(plan, branches, users));

    [Fact]
    public void EgyptExtrasCap_DoesNotApplyToRomaHrOrEnterprise()
    {
        Assert.Null(SubscriptionPriceList.EgyptExtrasCap(SubscriptionPriceList.PeoplePlanCode, int.MaxValue, 25));
        Assert.Null(SubscriptionPriceList.EgyptExtrasCap("enterprise", int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void OnlyEgyptHasAFoundingOffer()
    {
        foreach (var plan in new[] { "essential", "business", "professional", "enterprise" })
        {
            Assert.Equal(0, SubscriptionPriceList.Find(plan, "SAR")!.FoundingBase);
            Assert.Equal(0, SubscriptionPriceList.Find(plan, "GBP")!.FoundingBase);
        }
    }

    [Theory]
    [InlineData(Country.Egypt, "EGP")]
    [InlineData(Country.SaudiArabia, "SAR")]
    [InlineData(Country.UAE, "AED")]
    [InlineData(Country.Bahrain, "BHD")]
    [InlineData(Country.Oman, "OMR")]
    [InlineData(Country.Qatar, "QAR")]
    [InlineData(Country.Kuwait, "KWD")]
    [InlineData(Country.UnitedKingdom, "GBP")]
    [InlineData(Country.Guernsey, "GBP")]
    public void EveryCountryIsBilledInItsOwnCurrency(Country country, string currency)
    {
        Assert.Equal(currency, SubscriptionPriceList.CurrencyFor(country));
        // ...and that currency has a price for every plan, so no country can fall through to a missing price.
        foreach (var plan in new[] { "essential", "business", "professional", "enterprise" })
            Assert.NotNull(SubscriptionPriceList.Find(plan, currency));
    }

    /// <summary>The pegged Gulf currencies must equal what the public page displays (SAR list x rate, rounded).</summary>
    [Theory]
    [InlineData("AED", "essential", 146)]
    [InlineData("AED", "business", 342)]
    [InlineData("AED", "professional", 636)]
    [InlineData("QAR", "essential", 145)]
    [InlineData("QAR", "business", 339)]
    [InlineData("QAR", "professional", 630)]
    [InlineData("KWD", "essential", 12.2)]
    [InlineData("KWD", "business", 28.6)]
    [InlineData("KWD", "professional", 53.2)]
    [InlineData("BHD", "essential", 14.9)]
    [InlineData("BHD", "business", 34.9)]
    [InlineData("BHD", "professional", 64.9)]
    [InlineData("OMR", "essential", 15.3)]
    [InlineData("OMR", "business", 35.8)]
    [InlineData("OMR", "professional", 66.5)]
    public void GulfPricesMatchThePublicPage(string currency, string plan, double expected)
        => Assert.Equal((decimal)expected, SubscriptionPriceList.Find(plan, currency)!.Base);

    [Theory]
    [InlineData("SAR", 2)]
    [InlineData("EGP", 0)]
    [InlineData("GBP", 2)]
    [InlineData("AED", 0)]
    [InlineData("QAR", 0)]
    [InlineData("KWD", 1)]
    [InlineData("BHD", 1)]
    [InlineData("OMR", 1)]
    public void InvoiceDecimalsPerCurrency(string currency, int decimals)
        => Assert.Equal(decimals, SubscriptionPriceList.DecimalsFor(currency));

    /// <summary>The standalone HR plan: SAR 99 (matches marketing/roma-hr.html) with a 49.99 launch price, no branch
    /// overage, and a price in every billing currency.</summary>
    [Fact]
    public void PeoplePlanPricesMatchTheHrPage()
    {
        var sar = SubscriptionPriceList.Find(SubscriptionPriceList.PeoplePlanCode, "SAR")!;
        Assert.Equal(99m, sar.Base);
        Assert.Equal(49.99m, sar.FoundingBase);
        Assert.Equal(0m, sar.ExtraBranch);
        foreach (var currency in new[] { "SAR", "EGP", "GBP", "AED", "QAR", "KWD", "BHD", "OMR" })
        {
            var p = SubscriptionPriceList.Find(SubscriptionPriceList.PeoplePlanCode, currency);
            Assert.NotNull(p);
            Assert.True(p!.FoundingBase > 0 && p.FoundingBase < p.Base, currency);
        }
        Assert.Equal(97m, SubscriptionPriceList.Find(SubscriptionPriceList.PeoplePlanCode, "AED")!.Base);
    }

    [Fact]
    public void UnknownPlanHasNoPrice() => Assert.Null(SubscriptionPriceList.Find("nope", "SAR"));

    [Fact]
    public void Enterprise_HasNoPublishedPriceAndNoAutomaticDiscounts()
    {
        Assert.False(SubscriptionPriceList.SupportsAnnual("enterprise"));
        foreach (var currency in new[] { "SAR", "EGP", "GBP", "AED" })
        {
            var p = SubscriptionPriceList.Find("enterprise", currency)!;
            Assert.Equal(0m, p.Base);
            Assert.Equal(0m, p.FoundingBase);
        }
    }

    [Theory]
    [InlineData("people")]
    [InlineData("essential")]
    [InlineData("business")]
    [InlineData("professional")]
    public void FirstThreeErpPlansAndHrKeepTheAnnualDiscount(string plan)
        => Assert.True(SubscriptionPriceList.SupportsAnnual(plan));
}
