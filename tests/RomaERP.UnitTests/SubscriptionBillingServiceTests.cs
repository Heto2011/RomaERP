using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Billing;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class SubscriptionBillingServiceTests
{
    private class StubRegistry : ITenantRegistry
    {
        public Task<Tenant?> FindByCompanyCodeAsync(string companyCode, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public string BuildConnectionString(string databaseName) => string.Empty;
    }

    /// <summary>Same service, but usage (branches/users) comes from the test instead of a real tenant database.</summary>
    private class TestableBillingService : SubscriptionBillingService
    {
        public TestableBillingService(CentralDbContext central, IConfiguration configuration)
            : base(central, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                new StubRegistry(), Array.Empty<RomaERP.Application.Common.Interfaces.IPaymentGatewayProvider>(), configuration) { }

        public int Branches { get; set; } = 1;
        public int Users { get; set; } = 1;

        protected override Task<(int Branches, int Users)> CountTenantUsageAsync(Tenant tenant, CancellationToken ct)
            => Task.FromResult((Branches, Users));
    }

    private static CentralDbContext NewCentral()
        => new(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration Config(bool autoSuspend = false)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:AutoSuspendOverdue"] = autoSuspend.ToString(),
        }).Build();

    private static SubscriptionPlan Plan(string code, int branches, int users) => new()
    {
        Code = code, NameAr = code, NameEn = code, MonthlyBasePrice = 149, IncludedBranches = branches, IncludedUsers = users,
    };

    private static (Tenant Tenant, Subscription Sub) AddTenant(
        CentralDbContext central, SubscriptionPlan plan, Country country, SubscriptionStatus status, DateTime periodEnd, bool isDemo = false)
    {
        var tenant = new Tenant
        {
            CompanyCode = "t" + Guid.NewGuid().ToString("N")[..6], CompanyNameAr = "شركة", CompanyNameEn = "Co " + country,
            Country = country, DatabaseName = "db", IsDemo = isDemo, ExpiresAtUtc = isDemo ? periodEnd : null, IsActive = !isDemo,
        };
        var sub = new Subscription
        {
            TenantId = tenant.Id, PlanId = plan.Id, Status = status,
            CurrentPeriodStart = periodEnd.AddMonths(-1), CurrentPeriodEnd = periodEnd,
        };
        central.Tenants.Add(tenant);
        central.Subscriptions.Add(sub);
        central.SaveChanges();
        return (tenant, sub);
    }

    [Fact]
    public async Task ActivatePaid_TurnsAnExpiredTrialIntoAPermanentPayingTenant()
    {
        var central = NewCentral();
        var plan = Plan("essential", 3, 10);
        central.SubscriptionPlans.Add(plan);
        // A Saudi trial that has already expired and been locked by the expiry sweep.
        var (tenant, _) = AddTenant(central, plan, Country.SaudiArabia, SubscriptionStatus.Trialing, DateTime.UtcNow.AddDays(-2), isDemo: true);
        var service = new TestableBillingService(central, Config());

        var dto = await service.ActivatePaidAsync(tenant.Id, plan.Id, "TRF-1");

        var saved = await central.Tenants.SingleAsync();
        Assert.False(saved.IsDemo);                 // otherwise the expiry sweep would lock them again within hours
        Assert.Null(saved.ExpiresAtUtc);
        Assert.True(saved.IsActive);
        Assert.Equal(SubscriptionStatus.Active, dto.Status);

        var invoice = await central.SubscriptionInvoices.SingleAsync();
        Assert.Equal(SubscriptionInvoiceStatus.Paid, invoice.Status);
        Assert.Equal("TRF-1", invoice.PaymentReference);
        Assert.Equal("SAR", invoice.Currency);
        Assert.Equal(149m, invoice.TotalAmount);

        var sub = await central.Subscriptions.SingleAsync();
        Assert.True(sub.CurrentPeriodEnd > DateTime.UtcNow.AddDays(27));   // a full month is now paid for
    }

    [Fact]
    public async Task ActivatePaid_EgyptianCustomerGetsFoundingPriceInEgp()
    {
        var central = NewCentral();
        var plan = Plan("essential", 3, 10);
        central.SubscriptionPlans.Add(plan);
        var (tenant, _) = AddTenant(central, plan, Country.Egypt, SubscriptionStatus.Trialing, DateTime.UtcNow.AddDays(-1), isDemo: true);
        var service = new TestableBillingService(central, Config());

        await service.ActivatePaidAsync(tenant.Id, plan.Id, null);

        var invoice = await central.SubscriptionInvoices.SingleAsync();
        Assert.Equal("EGP", invoice.Currency);
        Assert.Equal(1500m, invoice.TotalAmount);   // 50% launch price for an early Egyptian customer
    }

    [Fact]
    public async Task BillingCycle_NeverInvoicesAnExpiredTrial()
    {
        var central = NewCentral();
        var plan = Plan("essential", 3, 10);
        central.SubscriptionPlans.Add(plan);
        AddTenant(central, plan, Country.Egypt, SubscriptionStatus.Trialing, DateTime.UtcNow.AddDays(-5), isDemo: true);
        var service = new TestableBillingService(central, Config());

        var result = await service.RunBillingCycleAsync();

        Assert.Equal(0, result.InvoicesGenerated);
        Assert.Empty(central.SubscriptionInvoices);
    }

    [Fact]
    public async Task BillingCycle_InvoicesADueActiveSubscriptionOnceAndAdvancesItsPeriod()
    {
        var central = NewCentral();
        var plan = Plan("business", 7, 25);
        central.SubscriptionPlans.Add(plan);
        AddTenant(central, plan, Country.UAE, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var service = new TestableBillingService(central, Config());

        var first = await service.RunBillingCycleAsync();
        var second = await service.RunBillingCycleAsync();   // the scheduled job runs several times a day

        Assert.Equal(1, first.InvoicesGenerated);
        Assert.Equal(0, second.InvoicesGenerated);
        var invoice = await central.SubscriptionInvoices.SingleAsync();
        Assert.Equal("AED", invoice.Currency);
        Assert.Equal(342m, invoice.TotalAmount);
        Assert.Equal(SubscriptionInvoiceStatus.Pending, invoice.Status);
    }

    private static async Task<(CentralDbContext Central, TestableBillingService Service, Tenant Tenant)> OverdueSetupAsync(bool autoSuspend)
    {
        var central = NewCentral();
        var plan = Plan("essential", 3, 10);
        central.SubscriptionPlans.Add(plan);
        var (tenant, sub) = AddTenant(central, plan, Country.SaudiArabia, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20));
        central.SubscriptionInvoices.Add(new SubscriptionInvoice
        {
            TenantId = tenant.Id, SubscriptionId = sub.Id, PlanCode = "essential", PlanNameAr = "x", TotalAmount = 149, Currency = "SAR",
            Status = SubscriptionInvoiceStatus.Pending, DueDateUtc = DateTime.UtcNow.AddDays(-20),   // well past the 7-day grace period
        });
        await central.SaveChangesAsync();
        return (central, new TestableBillingService(central, Config(autoSuspend)), tenant);
    }

    [Fact]
    public async Task BillingCycle_ByDefault_OnlyWarnsAboutAnOverdueTenantAndNeverSuspendsIt()
    {
        var (central, service, _) = await OverdueSetupAsync(autoSuspend: false);

        var result = await service.RunBillingCycleAsync();

        Assert.Equal(0, result.Suspended);
        Assert.True((await central.Tenants.SingleAsync()).IsActive);
        Assert.Contains(result.Notes, n => n.Contains("متأخر"));
    }

    [Fact]
    public async Task BillingCycle_WhenAutoSuspendIsSwitchedOn_SuspendsTheOverdueTenant()
    {
        var (central, service, _) = await OverdueSetupAsync(autoSuspend: true);

        var result = await service.RunBillingCycleAsync();

        Assert.Equal(1, result.Suspended);
        Assert.False((await central.Tenants.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task TenantList_ReportsHowManyDaysAnInvoiceIsOverdue()
    {
        var (_, service, _) = await OverdueSetupAsync(autoSuspend: false);

        var list = await service.GetTenantSubscriptionsAsync();

        Assert.InRange(list.Single().OverdueDays, 19, 21);
    }
}
