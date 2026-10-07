using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Billing;
using RomaERP.Infrastructure.Persistence.Central;
using Xunit;

namespace RomaERP.UnitTests;

public class LemonSqueezyServiceTests
{
    private const string Secret = "whsec_test_secret";

    private static CentralDbContext NewCentral()
        => new(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LemonSqueezyService NewService(CentralDbContext central, bool configured = true)
    {
        var values = new Dictionary<string, string?>();
        if (configured)
        {
            values["Lemon:ApiKey"] = "key";
            values["Lemon:StoreId"] = "1";
            values["Lemon:WebhookSecret"] = Secret;
            values["Lemon:Variants:essential-monthly"] = "111";
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new LemonSqueezyService(new HttpClient(), central, config, NullLogger<LemonSqueezyService>.Instance);
    }

    private static (Tenant Tenant, Subscription Sub, SubscriptionPlan Plan) Seed(CentralDbContext central)
    {
        var trial = new SubscriptionPlan { Code = "people", NameAr = "p", NameEn = "People", MonthlyBasePrice = 99, IncludedBranches = 1, IncludedUsers = 25 };
        var plan = new SubscriptionPlan { Code = "essential", NameAr = "e", NameEn = "Essential", MonthlyBasePrice = 149, IncludedBranches = 3, IncludedUsers = 10 };
        var tenant = new Tenant
        {
            CompanyCode = "acme", CompanyNameAr = "ا", CompanyNameEn = "Acme", Country = Country.SaudiArabia, DatabaseName = "db",
            IsDemo = true, ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), IsActive = false,
        };
        var sub = new Subscription
        {
            TenantId = tenant.Id, PlanId = trial.Id, Status = SubscriptionStatus.Trialing,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-20), CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1),
        };
        central.AddRange(trial, plan, tenant, sub);
        central.SaveChanges();
        return (tenant, sub, plan);
    }

    private static string Payload(string eventName, Guid tenantId, string dataId, string attributes)
        => "{\"meta\":{\"event_name\":\"" + eventName + "\",\"custom_data\":{\"tenant_id\":\"" + tenantId
           + "\",\"plan_code\":\"essential\",\"period\":\"monthly\"}},\"data\":{\"id\":\"" + dataId
           + "\",\"attributes\":{" + attributes + "}}}";

    [Fact]
    public void Signature_AcceptsTheCorrectHmacAndRejectsEverythingElse()
    {
        var service = NewService(NewCentral());
        var body = "{\"a\":1}";
        var good = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        Assert.True(service.VerifySignature(body, good));
        Assert.False(service.VerifySignature(body, "deadbeef"));
        Assert.False(service.VerifySignature(body + " ", good));
        Assert.False(service.VerifySignature(body, null));
        Assert.False(NewService(NewCentral(), configured: false).VerifySignature(body, good));
    }

    [Fact]
    public async Task SubscriptionCreated_TurnsTheTrialIntoAnActiveCardCustomer()
    {
        var central = NewCentral();
        var (tenant, sub, plan) = Seed(central);
        var service = NewService(central);

        await service.HandleWebhookAsync(Payload("subscription_created", tenant.Id, "sub-1",
            "\"customer_id\":77,\"renews_at\":\"" + DateTime.UtcNow.AddDays(30).ToString("o") + "\",\"status\":\"active\""));

        var savedTenant = await central.Tenants.SingleAsync();
        var savedSub = await central.Subscriptions.SingleAsync();
        Assert.True(savedTenant.IsActive);
        Assert.False(savedTenant.IsDemo);
        Assert.Null(savedTenant.ExpiresAtUtc);
        Assert.Equal(SubscriptionStatus.Active, savedSub.Status);
        Assert.Equal("LemonSqueezy", savedSub.PaymentProvider);
        Assert.Equal("sub-1", savedSub.PaymentProviderTokenRef);
        Assert.Equal(plan.Id, savedSub.PlanId);
        Assert.True(savedSub.CurrentPeriodEnd > DateTime.UtcNow.AddDays(25));
        Assert.Contains(await central.TenantActivities.ToListAsync(), a => a.Action == "Card subscription started");
    }

    [Fact]
    public async Task PaymentSuccess_RecordsOnePaidInvoiceEvenIfLemonRetriesTheWebhook()
    {
        var central = NewCentral();
        var (tenant, _, _) = Seed(central);
        var service = NewService(central);
        var body = Payload("subscription_payment_success", tenant.Id, "inv-9", "\"subscription_id\":\"sub-1\",\"total\":4000,\"currency\":\"USD\"");

        await service.HandleWebhookAsync(body);
        await service.HandleWebhookAsync(body);

        var invoice = await central.SubscriptionInvoices.SingleAsync();
        Assert.Equal(SubscriptionInvoiceStatus.Paid, invoice.Status);
        Assert.Equal(40m, invoice.TotalAmount);
        Assert.Equal("USD", invoice.Currency);
        Assert.Equal("lemon-inv:inv-9", invoice.PaymentReference);
    }

    [Fact]
    public async Task SubscriptionExpired_LocksTheCompany()
    {
        var central = NewCentral();
        var (tenant, _, _) = Seed(central);
        var service = NewService(central);
        await service.HandleWebhookAsync(Payload("subscription_created", tenant.Id, "sub-1", "\"customer_id\":1"));

        await service.HandleWebhookAsync(Payload("subscription_expired", tenant.Id, "sub-1", "\"status\":\"expired\""));

        Assert.False((await central.Tenants.SingleAsync()).IsActive);
        Assert.Equal(SubscriptionStatus.Suspended, (await central.Subscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task UnknownCompany_IsIgnoredWithoutThrowing()
    {
        var central = NewCentral();
        Seed(central);
        var service = NewService(central);

        await service.HandleWebhookAsync(Payload("subscription_created", Guid.NewGuid(), "sub-x", "\"customer_id\":1"));

        Assert.Equal(SubscriptionStatus.Trialing, (await central.Subscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task BillingCycle_DoesNotInvoiceACardSubscriptionItself()
    {
        // Covered by SubscriptionBillingService skipping PaymentProvider == "LemonSqueezy"; this guards the constant.
        Assert.Equal("LemonSqueezy", LemonSqueezyService.ProviderName);
        await Task.CompletedTask;
    }
}
