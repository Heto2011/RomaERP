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
    [InlineData("essential", "EGP", 3000)]
    [InlineData("business", "EGP", 7000)]
    [InlineData("professional", "EGP", 12000)]
    [InlineData("essential", "GBP", 49)]
    [InlineData("business", "GBP", 99)]
    [InlineData("professional", "GBP", 179)]
    public void PublishedBasePrices(string plan, string currency, int expected)
        => Assert.Equal(expected, SubscriptionPriceList.Find(plan, currency)!.Base);

    [Theory]
    [InlineData("essential", 1500)]
    [InlineData("business", 3500)]
    [InlineData("professional", 6000)]
    public void EgyptFoundingPriceIsHalfOfList(string plan, int founding)
    {
        var egp = SubscriptionPriceList.Find(plan, "EGP")!;
        Assert.Equal(founding, egp.FoundingBase);
        Assert.Equal(egp.Base / 2, egp.FoundingBase);
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

    [Fact]
    public void CurrencyFollowsTheTenantsCountry()
    {
        Assert.Equal("EGP", SubscriptionPriceList.CurrencyFor(Country.Egypt));
        Assert.Equal("SAR", SubscriptionPriceList.CurrencyFor(Country.SaudiArabia));
        Assert.Equal("SAR", SubscriptionPriceList.CurrencyFor(Country.UAE));
        Assert.Equal("GBP", SubscriptionPriceList.CurrencyFor(Country.UnitedKingdom));
    }

    [Fact]
    public void UnknownPlanHasNoPrice() => Assert.Null(SubscriptionPriceList.Find("nope", "SAR"));
}
