using RomaERP.Application.Tenancy;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Seed;
using Xunit;

namespace RomaERP.UnitTests;

public class TenancyTests
{
    [Fact]
    public void BuildAccounts_IncludesInputAndOutputVatAccounts()
    {
        var accounts = ChartOfAccountsFactory.BuildAccounts();

        Assert.Contains(accounts, a => a.Code == "1180" && a.NameAr.Contains("مدخلات"));
        Assert.Contains(accounts, a => a.Code == "2161" && a.NameAr.Contains("مخرجات"));
    }

    [Fact]
    public void BuildAccounts_ProducesNoDuplicateCodes()
    {
        var accounts = ChartOfAccountsFactory.BuildAccounts();

        var duplicateCodes = accounts.GroupBy(a => a.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicateCodes);
    }

    [Theory]
    [InlineData(Country.Egypt, 0.14, "EGP")]
    [InlineData(Country.SaudiArabia, 0.15, "SAR")]
    [InlineData(Country.UAE, 0.05, "AED")]
    [InlineData(Country.Bahrain, 0.10, "BHD")]
    [InlineData(Country.Oman, 0.05, "OMR")]
    [InlineData(Country.Qatar, 0.0, "QAR")]
    [InlineData(Country.Kuwait, 0.0, "KWD")]
    public void CountryTaxDefaults_ReturnsExpectedRateAndCurrency(Country country, double expectedRate, string expectedCurrency)
    {
        var result = CountryTaxDefaults.Get(country);

        Assert.Equal((decimal)expectedRate, result.VatRate);
        Assert.Equal(expectedCurrency, result.Currency);
    }
}
