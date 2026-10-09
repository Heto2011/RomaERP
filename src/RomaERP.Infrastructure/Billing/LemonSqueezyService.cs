using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.Infrastructure.Billing;

public class LemonSqueezyService : ILemonSqueezyService
{
    public const string ProviderName = "LemonSqueezy";
    private const string Actor = "Lemon Squeezy";

    private readonly HttpClient _http;
    private readonly CentralDbContext _central;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LemonSqueezyService> _logger;

    public LemonSqueezyService(HttpClient http, CentralDbContext central, IConfiguration configuration, ILogger<LemonSqueezyService> logger)
    {
        _http = http;
        _central = central;
        _configuration = configuration;
        _logger = logger;
    }

    private string? ApiKey => _configuration["Lemon:ApiKey"];
    private string? StoreId => _configuration["Lemon:StoreId"];
    private string? WebhookSecret => _configuration["Lemon:WebhookSecret"];

    // The store id is optional: when it isn't configured the API key's own (only) store is looked up once and remembered.
    private static string? _discoveredStoreId;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(WebhookSecret)
        && _configuration.GetSection("Lemon:Variants").GetChildren().Any(c => !string.IsNullOrWhiteSpace(c.Value));

    public static string VariantKey(string planCode, bool annual, bool uk = false)
        => $"{planCode.ToLowerInvariant()}-{(annual ? "annual" : "monthly")}{(uk ? "-uk" : "")}";

    private string? VariantId(string planCode, bool annual, bool uk)
        => _configuration[$"Lemon:Variants:{VariantKey(planCode, annual, uk)}"] is { Length: > 0 } v ? v.Trim() : null;

    public bool HasVariant(string planCode, bool annual, bool uk = false) => VariantId(planCode, annual, uk) is not null;

    public async Task<string> CreateCheckoutAsync(LemonCheckoutRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new ValidationAppException("الدفع بالبطاقة لسه مش مفعّل.");
        var variant = VariantId(request.PlanCode, request.Annual, request.Uk)
            ?? throw new ValidationAppException("الباقة دي مش متاحة للدفع بالبطاقة حاليًا.");

        var storeId = !string.IsNullOrWhiteSpace(StoreId) ? StoreId.Trim() : await DiscoverStoreIdAsync(ct);

        var body = new
        {
            data = new
            {
                type = "checkouts",
                attributes = new
                {
                    checkout_data = new
                    {
                        email = request.Email,
                        name = request.Name,
                        custom = new Dictionary<string, string>
                        {
                            ["tenant_id"] = request.TenantId.ToString(),
                            ["company_code"] = request.CompanyCode,
                            ["plan_code"] = request.PlanCode.ToLowerInvariant(),
                            ["period"] = request.Annual ? "annual" : "monthly",
                        },
                    },
                    product_options = new { redirect_url = request.RedirectUrl },
                },
                relationships = new
                {
                    store = new { data = new { type = "stores", id = storeId } },
                    variant = new { data = new { type = "variants", id = variant } },
                },
            },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.lemonsqueezy.com/v1/checkouts")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/vnd.api+json"),
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey!.Trim());

        using var response = await _http.SendAsync(message, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Lemon Squeezy checkout creation failed: {Status} {Body}", (int)response.StatusCode, text.Length > 500 ? text[..500] : text);
            throw new ValidationAppException("تعذر فتح صفحة الدفع دلوقتي — جرّب تاني بعد شوية أو كلّم الدعم.");
        }

        using var doc = JsonDocument.Parse(text);
        var url = doc.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("url").GetString();
        return string.IsNullOrWhiteSpace(url) ? throw new ValidationAppException("تعذر فتح صفحة الدفع دلوقتي.") : url;
    }

    private async Task<string> DiscoverStoreIdAsync(CancellationToken ct)
    {
        if (_discoveredStoreId is not null) return _discoveredStoreId;

        using var message = new HttpRequestMessage(HttpMethod.Get, "https://api.lemonsqueezy.com/v1/stores?page[size]=1");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey!.Trim());
        using var response = await _http.SendAsync(message, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Lemon Squeezy store lookup failed: {Status} {Body}", (int)response.StatusCode, text.Length > 300 ? text[..300] : text);
            throw new ValidationAppException("تعذر الاتصال بنظام الدفع دلوقتي — جرّب تاني بعد شوية أو كلّم الدعم.");
        }

        using var doc = JsonDocument.Parse(text);
        var first = doc.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
        var id = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        if (string.IsNullOrWhiteSpace(id))
            throw new ValidationAppException("مفيش متجر مرتبط بمفتاح الدفع.");
        return _discoveredStoreId = id;
    }

    public bool VerifySignature(string rawBody, string? signature)
    {
        var secret = WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature)) return false;

        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim()), Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var provided = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return provided.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(provided, expectedBytes);
    }

    public async Task HandleWebhookAsync(string rawBody, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var meta = root.GetProperty("meta");
        var eventName = meta.TryGetProperty("event_name", out var en) ? en.GetString() ?? "" : "";
        var data = root.GetProperty("data");
        var attrs = data.GetProperty("attributes");
        var dataId = data.TryGetProperty("id", out var idEl) ? idEl.ToString() : "";
        var custom = meta.TryGetProperty("custom_data", out var cd) && cd.ValueKind == JsonValueKind.Object ? cd : default;
        var testMode = attrs.TryGetProperty("test_mode", out var tm) && tm.ValueKind == JsonValueKind.True;

        // Payment events carry the subscription's id in an attribute; subscription events use the data id itself.
        var lemonSubscriptionId = eventName.StartsWith("subscription_payment_", StringComparison.Ordinal)
            ? Str(attrs, "subscription_id") ?? ""
            : dataId;

        var (tenant, subscription) = await ResolveAsync(custom, lemonSubscriptionId, ct);
        if (tenant is null || subscription is null)
        {
            _logger.LogWarning("Lemon webhook {Event} could not be matched to a company (subscription {SubscriptionId}).", eventName, lemonSubscriptionId);
            return;
        }

        var tag = testMode ? " (test mode)" : "";
        switch (eventName)
        {
            case "subscription_created":
                await OnCreatedAsync(tenant, subscription, custom, attrs, dataId, tag, ct);
                break;

            case "subscription_updated":
                ApplyRenewal(subscription, attrs);
                MapStatus(tenant, subscription, Str(attrs, "status"), tag);
                break;

            case "subscription_cancelled":
                ApplyEnd(subscription, attrs);
                Log(tenant, "Billing", "Card subscription cancelled", $"stays active until {subscription.CurrentPeriodEnd:yyyy-MM-dd}{tag}");
                break;

            case "subscription_resumed":
            case "subscription_unpaused":
                subscription.Status = SubscriptionStatus.Active;
                ApplyRenewal(subscription, attrs);
                Log(tenant, "Billing", "Card subscription resumed", tag.Trim());
                break;

            case "subscription_expired":
                subscription.Status = SubscriptionStatus.Suspended;
                subscription.SuspendedAtUtc = DateTime.UtcNow;
                tenant.IsActive = false;
                Log(tenant, "Status", "Company locked — card subscription expired", tag.Trim());
                break;

            case "subscription_payment_failed":
                subscription.Status = SubscriptionStatus.PastDue;
                Log(tenant, "Billing", "Card payment failed", tag.Trim());
                break;

            case "subscription_payment_success":
                await OnPaymentSuccessAsync(tenant, subscription, attrs, dataId, tag, ct);
                break;

            case "subscription_payment_refunded":
                Log(tenant, "Billing", "Card payment refunded", $"{Money(attrs)}{tag}");
                break;

            default:
                return; // an event we don't act on — nothing to save
        }

        await _central.SaveChangesAsync(ct);
    }

    private async Task<(Tenant?, Subscription?)> ResolveAsync(JsonElement custom, string lemonSubscriptionId, CancellationToken ct)
    {
        Tenant? tenant = null;
        Subscription? subscription = null;
        if (custom.ValueKind == JsonValueKind.Object && Str(custom, "tenant_id") is { } tid && Guid.TryParse(tid, out var tenantId))
        {
            tenant = await _central.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant is not null)
                subscription = await _central.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenant.Id, ct);
        }
        if (subscription is null && lemonSubscriptionId.Length > 0)
        {
            subscription = await _central.Subscriptions.FirstOrDefaultAsync(
                s => s.PaymentProvider == ProviderName && s.PaymentProviderTokenRef == lemonSubscriptionId, ct);
            if (subscription is not null)
                tenant = await _central.Tenants.FirstOrDefaultAsync(t => t.Id == subscription.TenantId, ct);
        }
        return (tenant, subscription);
    }

    private async Task OnCreatedAsync(Tenant tenant, Subscription subscription, JsonElement custom, JsonElement attrs, string lemonSubscriptionId, string tag, CancellationToken ct)
    {
        var planCode = custom.ValueKind == JsonValueKind.Object ? Str(custom, "plan_code") : null;
        var plan = planCode is null ? null : await _central.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == planCode, ct);
        var annual = custom.ValueKind == JsonValueKind.Object && Str(custom, "period") == "annual";

        // A first confirmed payment turns a trial into a customer for good (same as the owner confirming it by hand).
        tenant.IsDemo = false;
        tenant.ExpiresAtUtc = null;
        tenant.IsActive = true;

        if (plan is not null) subscription.PlanId = plan.Id;
        subscription.Status = SubscriptionStatus.Active;
        subscription.SuspendedAtUtc = null;
        subscription.PaymentProvider = ProviderName;
        subscription.PaymentProviderCustomerRef = Str(attrs, "customer_id");
        subscription.PaymentProviderTokenRef = lemonSubscriptionId;
        subscription.BillingPeriod = annual ? BillingPeriod.Annual : BillingPeriod.Monthly;
        subscription.CurrentPeriodStart = DateTime.UtcNow;
        subscription.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(annual ? SubscriptionPriceList.AnnualMonthsCovered : 1);
        ApplyRenewal(subscription, attrs);
        Log(tenant, "Billing", "Card subscription started", $"{planCode ?? "?"} · {(annual ? "annual" : "monthly")}{tag}");
    }

    private async Task OnPaymentSuccessAsync(Tenant tenant, Subscription subscription, JsonElement attrs, string lemonInvoiceId, string tag, CancellationToken ct)
    {
        var reference = $"lemon-inv:{lemonInvoiceId}";
        if (await _central.SubscriptionInvoices.AnyAsync(i => i.TenantId == tenant.Id && i.PaymentReference == reference, ct))
            return; // Lemon retries webhooks — never record the same payment twice.

        var plan = await _central.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == subscription.PlanId, ct);
        var cents = attrs.TryGetProperty("total", out var t) && t.TryGetInt64(out var c) ? c : 0;
        var amount = Math.Round(cents / 100m, 2);
        var now = DateTime.UtcNow;

        _central.SubscriptionInvoices.Add(new SubscriptionInvoice
        {
            TenantId = tenant.Id,
            SubscriptionId = subscription.Id,
            PlanCode = plan?.Code ?? "",
            PlanNameAr = plan?.NameAr ?? "",
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
            BaseAmount = amount,
            TotalAmount = amount,
            Currency = (Str(attrs, "currency") ?? "USD").ToUpperInvariant(),
            Status = SubscriptionInvoiceStatus.Paid,
            DueDateUtc = now,
            PaidAtUtc = now,
            PaymentReference = reference,
            Notes = "Card payment via Lemon Squeezy" + tag,
        });

        if (subscription.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Trialing)
            subscription.Status = SubscriptionStatus.Active;
        Log(tenant, "Billing", "Card payment received", $"{Money(attrs)}{tag}");
    }

    private void MapStatus(Tenant tenant, Subscription subscription, string? lemonStatus, string tag)
    {
        switch (lemonStatus)
        {
            case "active" or "on_trial":
                if (subscription.Status is SubscriptionStatus.PastDue) subscription.Status = SubscriptionStatus.Active;
                break;
            case "past_due" or "unpaid":
                if (subscription.Status != SubscriptionStatus.PastDue)
                {
                    subscription.Status = SubscriptionStatus.PastDue;
                    Log(tenant, "Billing", "Card subscription past due", tag.Trim());
                }
                break;
            case "expired":
                subscription.Status = SubscriptionStatus.Suspended;
                subscription.SuspendedAtUtc = DateTime.UtcNow;
                tenant.IsActive = false;
                break;
        }
    }

    private static void ApplyRenewal(Subscription subscription, JsonElement attrs)
    {
        if (ParseDate(attrs, "renews_at") is { } renews && renews > DateTime.UtcNow.AddHours(-1))
            subscription.CurrentPeriodEnd = renews;
    }

    private static void ApplyEnd(Subscription subscription, JsonElement attrs)
    {
        if (ParseDate(attrs, "ends_at") is { } ends)
            subscription.CurrentPeriodEnd = ends;
    }

    private static DateTime? ParseDate(JsonElement attrs, string name)
        => Str(attrs, name) is { } s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static string? Str(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private static string Money(JsonElement attrs)
        => attrs.TryGetProperty("total", out var t) && t.TryGetInt64(out var c) ? $"{c / 100m:0.00} {(Str(attrs, "currency") ?? "USD").ToUpperInvariant()}" : "";

    private void Log(Tenant tenant, string category, string action, string? details)
        => _central.TenantActivities.Add(TenantActivityLog.Build(tenant.Id, tenant.CompanyCode, category, action, details, Actor));
}
