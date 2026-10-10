using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Billing;
using RomaERP.Infrastructure.Persistence.Central;
using Xunit;

namespace RomaERP.UnitTests;

public class LemonExtrasTests
{
    private class RecordingHandler : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Body)> Calls = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var url = request.RequestUri!.ToString();
            Calls.Add((request.Method.Method, url, body));
            var json = url.Contains("/checkouts") ? "{\"data\":{\"attributes\":{\"url\":\"https://pay.example/extra\"}}}"
                : url.Contains("/subscription-items") && request.Method == HttpMethod.Get ? "{\"data\":[{\"id\":\"item-9\"}]}"
                : "{\"data\":{}}";
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    private static (LemonSqueezyService Service, CentralDbContext Central, RecordingHandler Handler, Tenant Tenant, Subscription Sub) Build(
        int paidBranches = 0, string? branchSubId = null, string plan = "essential")
    {
        var central = new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var essential = new SubscriptionPlan { Code = "essential", NameAr = "e", NameEn = "Essential", MonthlyBasePrice = 149, IncludedBranches = 3, IncludedUsers = 10 };
        var business = new SubscriptionPlan { Code = "business", NameAr = "b", NameEn = "Business", MonthlyBasePrice = 349, IncludedBranches = 7, IncludedUsers = 25 };
        var tenant = new Tenant { CompanyCode = "acme", CompanyNameAr = "ا", CompanyNameEn = "Acme", Country = Country.SaudiArabia, DatabaseName = "db" };
        var sub = new Subscription
        {
            TenantId = tenant.Id, PlanId = (plan == "business" ? business : essential).Id, Status = SubscriptionStatus.Active,
            PaymentProvider = "LemonSqueezy", PaymentProviderTokenRef = "sub-1",
            ExtraBranchesPaid = paidBranches, ExtraBranchesLemonSubscriptionId = branchSubId,
        };
        central.AddRange(essential, business, tenant, sub);
        central.SaveChanges();

        var handler = new RecordingHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Lemon:ApiKey"] = "key", ["Lemon:WebhookSecret"] = "s", ["Lemon:StoreId"] = "1",
            ["Lemon:Variants:extra-branch-monthly"] = "501", ["Lemon:Variants:extra-user-monthly"] = "502",
            ["Lemon:Variants:business-monthly"] = "601",
        }).Build();
        return (new LemonSqueezyService(new HttpClient(handler), central, config, NullLogger<LemonSqueezyService>.Instance), central, handler, tenant, sub);
    }

    [Fact]
    public async Task FirstExtra_OpensACheckoutForTheWantedQuantity()
    {
        var (service, _, handler, tenant, _) = Build();

        var result = await service.ChangeExtrasAsync(tenant.Id, ExtraKind.Branch, 2, 0, "a@b.co", "A", "https://x/r", uk: false);

        Assert.Equal("https://pay.example/extra", result.CheckoutUrl);
        var call = Assert.Single(handler.Calls);
        Assert.Contains("extra_branch", call.Body);
        Assert.Contains("\"quantity\":2", call.Body);
    }

    [Fact]
    public async Task LaterExtras_ChangeTheQuantityInPlaceAndInvoiceImmediately()
    {
        var (service, central, handler, tenant, _) = Build(paidBranches: 1, branchSubId: "xb-1");

        var result = await service.ChangeExtrasAsync(tenant.Id, ExtraKind.Branch, 2, 0, "a@b.co", "A", "https://x/r", uk: false);

        Assert.Null(result.CheckoutUrl);
        Assert.Equal(3, result.NewQuantity);
        var patch = handler.Calls.Single(c => c.Method == "PATCH");
        Assert.EndsWith("/subscription-items/item-9", patch.Url);
        Assert.Contains("\"quantity\":3", patch.Body);
        Assert.Contains("invoice_immediately", patch.Body);
        Assert.Equal(3, (await central.Subscriptions.FirstAsync()).ExtraBranchesPaid);
    }

    [Fact]
    public async Task RemovingTheLastExtra_CancelsTheAddOn_AndCannotGoBelowWhatIsUsed()
    {
        var (service, central, handler, tenant, _) = Build(paidBranches: 2, branchSubId: "xb-1");

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.ChangeExtrasAsync(tenant.Id, ExtraKind.Branch, -1, 2, "a@b.co", "A", "https://x/r", uk: false));

        await service.ChangeExtrasAsync(tenant.Id, ExtraKind.Branch, -2, 0, "a@b.co", "A", "https://x/r", uk: false);
        Assert.Contains(handler.Calls, c => c.Method == "DELETE" && c.Url.EndsWith("/subscriptions/xb-1"));
        var sub = await central.Subscriptions.FirstAsync();
        Assert.Equal(0, sub.ExtraBranchesPaid);
        Assert.Null(sub.ExtraBranchesLemonSubscriptionId);
    }

    [Fact]
    public async Task Upgrade_SwitchesVariantUpwardOnly()
    {
        var (service, central, handler, tenant, _) = Build();

        await service.ChangePlanAsync(tenant.Id, "business", uk: false);

        var patch = handler.Calls.Single(c => c.Method == "PATCH");
        Assert.EndsWith("/subscriptions/sub-1", patch.Url);
        Assert.Contains("\"variant_id\":601", patch.Body);
        var business = await central.SubscriptionPlans.FirstAsync(p => p.Code == "business");
        Assert.Equal(business.Id, (await central.Subscriptions.FirstAsync()).PlanId);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.ChangePlanAsync(tenant.Id, "essential", uk: false));
    }

    private static string ExtraPayload(string eventName, Guid tenantId, string id, string attrs)
        => "{\"meta\":{\"event_name\":\"" + eventName + "\",\"custom_data\":{\"tenant_id\":\"" + tenantId + "\",\"kind\":\"extra_user\"}},"
           + "\"data\":{\"id\":\"" + id + "\",\"attributes\":{" + attrs + "}}}";

    [Fact]
    public async Task ExtraWebhooks_SetQuantity_RecordPayments_AndEndOnExpiry()
    {
        var (service, central, _, tenant, _) = Build();

        await service.HandleWebhookAsync(ExtraPayload("subscription_created", tenant.Id, "xu-1", "\"status\":\"active\",\"first_subscription_item\":{\"quantity\":4}"));
        var sub = await central.Subscriptions.FirstAsync();
        Assert.Equal(4, sub.ExtraUsersPaid);
        Assert.Equal("xu-1", sub.ExtraUsersLemonSubscriptionId);
        Assert.Equal("sub-1", sub.PaymentProviderTokenRef); // the main subscription is untouched

        await service.HandleWebhookAsync(ExtraPayload("subscription_updated", tenant.Id, "xu-1", "\"status\":\"active\",\"first_subscription_item\":{\"quantity\":6}"));
        Assert.Equal(6, (await central.Subscriptions.FirstAsync()).ExtraUsersPaid);

        var payment = ExtraPayload("subscription_payment_success", tenant.Id, "inv-1", "\"subscription_id\":\"xu-1\",\"total\":3000,\"currency\":\"USD\"");
        await service.HandleWebhookAsync(payment);
        await service.HandleWebhookAsync(payment); // retried webhook
        var invoice = Assert.Single(await central.SubscriptionInvoices.ToListAsync());
        Assert.Equal(30m, invoice.TotalAmount);
        Assert.Equal("extra-user", invoice.PlanCode);

        await service.HandleWebhookAsync(ExtraPayload("subscription_expired", tenant.Id, "xu-1", "\"status\":\"expired\""));
        sub = await central.Subscriptions.FirstAsync();
        Assert.Equal(0, sub.ExtraUsersPaid);
        Assert.Null(sub.ExtraUsersLemonSubscriptionId);
        Assert.True((await central.Tenants.FirstAsync()).IsActive);
    }
}
