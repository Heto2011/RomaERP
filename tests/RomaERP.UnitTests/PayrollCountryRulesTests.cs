using RomaERP.Application.HR.Services.Countries;
using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class PayrollCountryRulesTests
{
    private static CompanySettings Settings(Country c, bool gosi = false) => new() { CompanyNameAr = "ش", CompanyNameEn = "Co", Country = c, GosiEnabled = gosi };

    [Theory]
    [InlineData(Country.UnitedKingdom, typeof(UnitedKingdomPayrollRule))]
    [InlineData(Country.Egypt, typeof(EgyptPayrollRule))]
    [InlineData(Country.Guernsey, typeof(GuernseyPayrollRule))]
    public void EachCountry_GetsOnlyItsOwnRules(Country country, Type expected)
    {
        var rules = PayrollCountryRules.For(Settings(country));
        Assert.Single(rules);
        Assert.IsType(expected, rules[0]);
    }

    [Fact]
    public void GosiIsItsOwnRule_AndOnlyWhenTurnedOn()
    {
        Assert.Empty(PayrollCountryRules.For(Settings(Country.SaudiArabia)));
        Assert.IsType<SaudiGosiRule>(Assert.Single(PayrollCountryRules.For(Settings(Country.SaudiArabia, gosi: true))));
    }

    [Fact]
    public void ACountryWithoutRules_GetsNone()
        => Assert.Empty(PayrollCountryRules.For(Settings(Country.UAE)));
}
