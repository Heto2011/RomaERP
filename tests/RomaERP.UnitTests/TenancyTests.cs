using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Tenancy;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Persistence.Seed;
using RomaERP.Infrastructure.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class TenancyTests
{
    private static CentralDbContext CreateCentralContext()
    {
        var options = new DbContextOptionsBuilder<CentralDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CentralDbContext(options);
    }

    [Fact]
    public async Task DeactivateExpiredDemoTenants_DeactivatesOnlyDemoTenantsPastExpiry()
    {
        var ctx = CreateCentralContext();
        var now = DateTime.UtcNow;

        var expiredDemo = new Tenant { CompanyCode = "expired-demo", CompanyNameAr = "أ", CompanyNameEn = "A", DatabaseName = "db1", IsActive = true, IsDemo = true, ExpiresAtUtc = now.AddDays(-1) };
        var futureDemo = new Tenant { CompanyCode = "future-demo", CompanyNameAr = "ب", CompanyNameEn = "B", DatabaseName = "db2", IsActive = true, IsDemo = true, ExpiresAtUtc = now.AddDays(5) };
        var expiredButAlreadyOff = new Tenant { CompanyCode = "already-off", CompanyNameAr = "ج", CompanyNameEn = "C", DatabaseName = "db3", IsActive = false, IsDemo = true, ExpiresAtUtc = now.AddDays(-10) };
        var realTenant = new Tenant { CompanyCode = "real-customer", CompanyNameAr = "د", CompanyNameEn = "D", DatabaseName = "db4", IsActive = true, IsDemo = false, ExpiresAtUtc = null };
        ctx.Tenants.AddRange(expiredDemo, futureDemo, expiredButAlreadyOff, realTenant);
        await ctx.SaveChangesAsync();

        var service = new TenantProvisioningService(ctx, null!, null!);
        var deactivatedCount = await service.DeactivateExpiredDemoTenantsAsync();

        Assert.Equal(1, deactivatedCount);

        var reloaded = await ctx.Tenants.AsNoTracking().ToListAsync();
        Assert.False(reloaded.Single(t => t.CompanyCode == "expired-demo").IsActive);
        Assert.True(reloaded.Single(t => t.CompanyCode == "future-demo").IsActive);
        Assert.True(reloaded.Single(t => t.CompanyCode == "real-customer").IsActive);
    }

    [Fact]
    public async Task GetTenants_DemoOnlyFiltersOutRealTenants()
    {
        var ctx = CreateCentralContext();
        ctx.Tenants.AddRange(
            new Tenant { CompanyCode = "demo-1", CompanyNameAr = "أ", CompanyNameEn = "A", DatabaseName = "db1", IsDemo = true },
            new Tenant { CompanyCode = "real-1", CompanyNameAr = "ب", CompanyNameEn = "B", DatabaseName = "db2", IsDemo = false }
        );
        await ctx.SaveChangesAsync();

        var service = new TenantProvisioningService(ctx, null!, null!);
        var demoTenants = await service.GetTenantsAsync(demoOnly: true);

        var tenant = Assert.Single(demoTenants);
        Assert.Equal("demo-1", tenant.CompanyCode);
    }

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
