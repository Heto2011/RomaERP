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

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(WebhookSecret);

    public static string VariantKey(string planCode, bool annual, bool uk = false)
        => $"{planCode.ToLowerInvariant()}-{(annual ? "annual" : "monthly")}{(uk ? "-uk" : "")}";

    // Variant ids found from the Lemon account itself (so nobody has to copy ids by hand). A Lemon:Variants entry
    // in the configuration, if present, overrides what was found. Refreshed every ten minutes.
    private static readonly object CacheLock = new();
    private static Dictionary<string, string> _discovered = new();
    private static DateTime _discoveredAt = DateTime.MinValue;

    private async Task<string?> VariantIdAsync(string planCode, bool annual, bool uk, CancellationToken ct)
    {
        var key = VariantKey(planCode, annual, uk);
        if (_configuration[$"Lemon:Variants:{key}"] is { Length: > 0 } configured) return configured.Trim();

        Dictionary<string, string> map;
        lock (CacheLock) map = _discovered;
        if (map.Count == 0 || DateTime.UtcNow - _discoveredAt > TimeSpan.FromMinutes(10))
        {
            try
            {
                map = await DiscoverVariantsAsync(ct);
                lock (CacheLock) { _discovered = map; _discoveredAt = DateTime.UtcNow; }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list the Lemon Squeezy variants.");
            }
        }
        return map.TryGetValue(key, out var id) ? id : null;
    }

    private async Task<Dictionary<string, string>> DiscoverVariantsAsync(CancellationToken ct)
    {
        var products = new Dictionary<string, string>(); // product id -> plan code
        using (var doc = await GetJsonAsync("https://api.lemonsqueezy.com/v1/products?page[size]=100", ct))
        {
            foreach (var p in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var name = Str(p.GetProperty("attributes"), "name")?.ToLowerInvariant() ?? "";
                var code = name.Contains("extra") && name.Contains("branch") ? "extra-branch"
                    : name.Contains("extra") && name.Contains("user") ? "extra-user"
                    : name.Contains("essential") ? "essential"
                    : name.Contains("business") ? "business"
                    : name.Contains("professional") ? "professional"
                    : name.Contains("hr") || name.Contains("people") ? "people" : null;
                if (code is not null) products[p.GetProperty("id").ToString()] = code;
            }
        }

        var map = new Dictionary<string, string>();
        using var variants = await GetJsonAsync("https://api.lemonsqueezy.com/v1/variants?page[size]=100", ct);
        foreach (var v in variants.RootElement.GetProperty("data").EnumerateArray())
        {
            var attrs = v.GetProperty("attributes");
            var productId = Str(attrs, "product_id");
            if (productId is null || !products.TryGetValue(productId, out var code)) continue;
            var variantName = Str(attrs, "name")?.Trim().ToLowerInvariant();
            var suffix = variantName switch
            {
                "monthly" => "monthly",
                "annual" => "annual",
                "uk monthly" => "monthly-uk",
                "uk annual" => "annual-uk",
                _ => null,
            };
            if (suffix is not null) map[$"{code}-{suffix}"] = v.GetProperty("id").ToString();
        }
        return map;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey!.Trim());
        using var response = await _http.SendAsync(message, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Lemon Squeezy {(int)response.StatusCode}: {(text.Length > 200 ? text[..200] : text)}");
        return JsonDocument.Parse(text);
    }

    public async Task<bool> HasVariantAsync(string planCode, bool annual, bool uk = false, CancellationToken ct = default)
        => IsConfigured && await VariantIdAsync(planCode, annual, uk, ct) is not null;

    public async Task<string> CreateCheckoutAsync(LemonCheckoutRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new ValidationAppException("الدفع بالبطاقة لسه مش مفعّل.");
        var variant = await VariantIdAsync(request.PlanCode, request.Annual, request.Uk, ct)
            ?? throw new ValidationAppException("الباقة دي مش متاحة للدفع بالبطاقة حاليًا.");

        // The Roma HR launch offer (50% off the first 3 monthly payments) is a Lemon discount code applied for the customer,
        // so nobody has to know or type it. Annual payments don't get it.
        var foundingCode = _configuration["Lemon:FoundingDiscountCode"]?.Trim();
        var applyFounding = !string.IsNullOrEmpty(foundingCode) && !request.Annual
            && string.Equals(request.PlanCode, "people", StringComparison.OrdinalIgnoreCase);

        return await PostCheckoutAsync(variant, request.Email, request.Name, request.RedirectUrl, applyFounding ? foundingCode : null, 1,
            new Dictionary<string, string>
            {
                ["tenant_id"] = request.TenantId.ToString(),
                ["company_code"] = request.CompanyCode,
                ["plan_code"] = request.PlanCode.ToLowerInvariant(),
                ["period"] = request.Annual ? "annual" : "monthly",
            }, ct);
    }

    private async Task<string> PostCheckoutAsync(string variant, string email, string name, string redirectUrl, string? discountCode, int quantity,
        Dictionary<string, string> custom, CancellationToken ct)
    {
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
                        email,
                        name,
                        discount_code = discountCode,
                        variant_quantities = quantity > 1 ? new[] { new { variant_id = long.Parse(variant, CultureInfo.InvariantCulture), quantity } } : null,
                        custom,
                    },
                    product_options = new { redirect_url = redirectUrl },
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
            Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }), Encoding.UTF8, "application/vnd.api+json"),
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

    // ---- Paid extras (branches / users / HR employees) and plan upgrades -------------------------------------------

    private static string ExtraProduct(ExtraKind kind) => kind == ExtraKind.Branch ? "extra-branch" : "extra-user";

    public async Task<ExtraChangeResult> ChangeExtrasAsync(Guid tenantId, ExtraKind kind, int delta, int minimumQuantity, string email, string name, string redirectUrl, bool uk, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new ValidationAppException("الدفع بالبطاقة لسه مش مفعّل.");
        var sub = await _central.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            ?? throw new ValidationAppException("مفيش اشتراك للشركة.");
        if (sub.PaymentProvider != ProviderName)
            throw new ValidationAppException("الإضافات بالبطاقة متاحة للشركات اللي بتدفع بالبطاقة بس.");

        var current = kind == ExtraKind.Branch ? sub.ExtraBranchesPaid : sub.ExtraUsersPaid;
        var existingId = kind == ExtraKind.Branch ? sub.ExtraBranchesLemonSubscriptionId : sub.ExtraUsersLemonSubscriptionId;
        var target = current + delta;
        if (target < Math.Max(0, minimumQuantity))
            throw new ValidationAppException("مش هتقدر تقلل الإضافات عن اللي مستخدمه فعلًا — امسح الأول فرع/مستخدم زيادة.");
        if (target > 500) throw new ValidationAppException("العدد كبير — كلّم الدعم.");
        if (target == current) return new ExtraChangeResult(null, current);

        var kindLabel = kind == ExtraKind.Branch ? "branches" : "users";
        var tenant = await _central.Tenants.FirstAsync(t => t.Id == tenantId, ct);

        if (string.IsNullOrEmpty(existingId))
        {
            if (target <= 0) return new ExtraChangeResult(null, 0);
            var variant = await VariantIdAsync(ExtraProduct(kind), false, uk, ct)
                ?? throw new ValidationAppException("الإضافة دي مش متاحة للدفع بالبطاقة حاليًا.");
            var url = await PostCheckoutAsync(variant, email, name, redirectUrl, null, target,
                new Dictionary<string, string>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["company_code"] = tenant.CompanyCode,
                    ["kind"] = kind == ExtraKind.Branch ? "extra_branch" : "extra_user",
                }, ct);
            return new ExtraChangeResult(url, current);
        }

        if (target == 0)
        {
            await SendAsync(HttpMethod.Delete, $"https://api.lemonsqueezy.com/v1/subscriptions/{existingId}", null, ct);
            if (kind == ExtraKind.Branch) { sub.ExtraBranchesPaid = 0; sub.ExtraBranchesLemonSubscriptionId = null; }
            else { sub.ExtraUsersPaid = 0; sub.ExtraUsersLemonSubscriptionId = null; }
            Log(tenant, "Billing", $"Extra {kindLabel} removed", "all extras cancelled (stay paid until the period ends)");
            await _central.SaveChangesAsync(ct);
            return new ExtraChangeResult(null, 0);
        }

        string itemId;
        using (var items = await GetJsonAsync($"https://api.lemonsqueezy.com/v1/subscription-items?filter[subscription_id]={existingId}", ct))
        {
            var first = items.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
            itemId = first.ValueKind == JsonValueKind.Object ? first.GetProperty("id").ToString() : "";
        }
        if (itemId.Length == 0) throw new ValidationAppException("تعذر تعديل الإضافة دلوقتي — جرّب تاني بعد شوية.");

        await SendAsync(new HttpMethod("PATCH"), $"https://api.lemonsqueezy.com/v1/subscription-items/{itemId}", new
        {
            data = new
            {
                type = "subscription-items",
                id = itemId,
                attributes = new { quantity = target, invoice_immediately = true },
            },
        }, ct);

        if (kind == ExtraKind.Branch) sub.ExtraBranchesPaid = target; else sub.ExtraUsersPaid = target;
        Log(tenant, "Billing", $"Extra {kindLabel} changed", $"{current} → {target}");
        await _central.SaveChangesAsync(ct);
        return new ExtraChangeResult(null, target);
    }

    private static readonly string[] PlanOrder = { "essential", "business", "professional" };

    public async Task ChangePlanAsync(Guid tenantId, string newPlanCode, bool uk, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new ValidationAppException("الدفع بالبطاقة لسه مش مفعّل.");
        var sub = await _central.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            ?? throw new ValidationAppException("مفيش اشتراك للشركة.");
        if (sub.PaymentProvider != ProviderName || string.IsNullOrEmpty(sub.PaymentProviderTokenRef))
            throw new ValidationAppException("الترقية الأوتوماتيك متاحة للشركات اللي بتدفع بالبطاقة بس.");

        var currentPlan = await _central.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == sub.PlanId, ct);
        var newCode = newPlanCode.Trim().ToLowerInvariant();
        var from = Array.IndexOf(PlanOrder, currentPlan.Code.ToLowerInvariant());
        var to = Array.IndexOf(PlanOrder, newCode);
        if (from < 0 || to < 0) throw new ValidationAppException("الباقة دي مش متاحة للترقية الأوتوماتيك.");
        if (to <= from) throw new ValidationAppException("الترقية لباقة أعلى بس. للتخفيض كلّم الدعم.");

        var newPlan = await _central.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == newCode, ct)
            ?? throw new ValidationAppException("الباقة مش موجودة.");
        var annual = sub.BillingPeriod == BillingPeriod.Annual;
        var variant = await VariantIdAsync(newCode, annual, uk, ct)
            ?? throw new ValidationAppException("الباقة دي مش متاحة للدفع بالبطاقة حاليًا.");

        await SendAsync(new HttpMethod("PATCH"), $"https://api.lemonsqueezy.com/v1/subscriptions/{sub.PaymentProviderTokenRef}", new
        {
            data = new
            {
                type = "subscriptions",
                id = sub.PaymentProviderTokenRef,
                attributes = new { variant_id = long.Parse(variant, CultureInfo.InvariantCulture), invoice_immediately = true },
            },
        }, ct);

        sub.PlanId = newPlan.Id;
        var tenant = await _central.Tenants.FirstAsync(t => t.Id == tenantId, ct);
        Log(tenant, "Billing", "Plan upgraded (card)", $"{currentPlan.Code} → {newCode}");
        await _central.SaveChangesAsync(ct);
    }

    private async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(method, url);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey!.Trim());
        if (body is not null) message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/vnd.api+json");
        using var response = await _http.SendAsync(message, ct);
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("Lemon Squeezy {Method} {Url} failed: {Status} {Body}", method, url, (int)response.StatusCode, text.Length > 500 ? text[..500] : text);
        throw new ValidationAppException("تعذر تنفيذ العملية دلوقتي — جرّب تاني بعد شوية أو كلّم الدعم.");
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

        // Events of the add-on subscriptions ("Extra Branch" / "Extra User") are told apart by the kind we put in the
        // checkout, or by the id we stored when that add-on was first paid for.
        var extra = ExtraKindOf(custom, subscription, lemonSubscriptionId);
        if (extra is { } extraKind)
        {
            if (await HandleExtraAsync(tenant, subscription, extraKind, eventName, attrs, dataId, lemonSubscriptionId, tag, ct))
                await _central.SaveChangesAsync(ct);
            return;
        }

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

    private static ExtraKind? ExtraKindOf(JsonElement custom, Subscription subscription, string lemonSubscriptionId)
    {
        switch (custom.ValueKind == JsonValueKind.Object ? Str(custom, "kind") : null)
        {
            case "extra_branch": return ExtraKind.Branch;
            case "extra_user": return ExtraKind.User;
        }
        if (lemonSubscriptionId.Length == 0) return null;
        if (lemonSubscriptionId == subscription.ExtraBranchesLemonSubscriptionId) return ExtraKind.Branch;
        if (lemonSubscriptionId == subscription.ExtraUsersLemonSubscriptionId) return ExtraKind.User;
        return null;
    }

    private static int ItemQuantity(JsonElement attrs)
        => attrs.TryGetProperty("first_subscription_item", out var item) && item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var n) ? Math.Max(0, n) : 0;

    /// <summary>Applies a webhook of an add-on subscription. Returns true when something changed and must be saved.</summary>
    private async Task<bool> HandleExtraAsync(Tenant tenant, Subscription sub, ExtraKind kind, string eventName, JsonElement attrs, string dataId,
        string lemonSubscriptionId, string tag, CancellationToken ct)
    {
        var branches = kind == ExtraKind.Branch;
        var label = branches ? "branches" : "users";
        var storedId = branches ? sub.ExtraBranchesLemonSubscriptionId : sub.ExtraUsersLemonSubscriptionId;

        void SetPaid(int quantity, string? id)
        {
            if (branches) { sub.ExtraBranchesPaid = quantity; sub.ExtraBranchesLemonSubscriptionId = id; }
            else { sub.ExtraUsersPaid = quantity; sub.ExtraUsersLemonSubscriptionId = id; }
        }

        // An add-on that was removed and bought again has a new Lemon id; late events of the old one must not touch it.
        if (eventName != "subscription_created" && !string.IsNullOrEmpty(storedId) && storedId != lemonSubscriptionId) return false;
        if (eventName != "subscription_created" && string.IsNullOrEmpty(storedId) && eventName != "subscription_payment_success") return false;

        switch (eventName)
        {
            case "subscription_created":
            {
                var quantity = Math.Max(1, ItemQuantity(attrs));
                SetPaid(quantity, dataId);
                Log(tenant, "Billing", $"Extra {label} added", $"{quantity} paid{tag}");
                return true;
            }
            case "subscription_updated":
            {
                var status = Str(attrs, "status");
                if (status is "expired" or "cancelled") return false; // the end is handled by the cancelled / expired events
                var quantity = ItemQuantity(attrs);
                if (quantity <= 0 || quantity == (branches ? sub.ExtraBranchesPaid : sub.ExtraUsersPaid)) return false;
                SetPaid(quantity, storedId);
                Log(tenant, "Billing", $"Extra {label} changed", $"{quantity} paid{tag}");
                return true;
            }
            case "subscription_expired":
                SetPaid(0, null);
                Log(tenant, "Billing", $"Extra {label} ended", tag.Trim());
                return true;
            case "subscription_cancelled":
                Log(tenant, "Billing", $"Extra {label} cancelled", $"paid until {ParseDate(attrs, "ends_at"):yyyy-MM-dd}{tag}");
                return true;
            case "subscription_payment_failed":
                Log(tenant, "Billing", $"Extra {label} payment failed", tag.Trim());
                return true;
            case "subscription_payment_refunded":
                Log(tenant, "Billing", $"Extra {label} payment refunded", $"{Money(attrs)}{tag}");
                return true;
            case "subscription_payment_success":
            {
                var reference = $"lemon-inv:{dataId}";
                if (await _central.SubscriptionInvoices.AnyAsync(i => i.TenantId == tenant.Id && i.PaymentReference == reference, ct)) return false;
                var cents = attrs.TryGetProperty("total", out var t) && t.TryGetInt64(out var c) ? c : 0;
                var amount = Math.Round(cents / 100m, 2);
                var now = DateTime.UtcNow;
                _central.SubscriptionInvoices.Add(new SubscriptionInvoice
                {
                    TenantId = tenant.Id,
                    SubscriptionId = sub.Id,
                    PlanCode = branches ? "extra-branch" : "extra-user",
                    PlanNameAr = branches ? "فروع إضافية" : "مستخدمين إضافيين",
                    PeriodStart = now,
                    PeriodEnd = now.AddMonths(1),
                    BaseAmount = amount,
                    TotalAmount = amount,
                    Currency = (Str(attrs, "currency") ?? "USD").ToUpperInvariant(),
                    Status = SubscriptionInvoiceStatus.Paid,
                    DueDateUtc = now,
                    PaidAtUtc = now,
                    PaymentReference = reference,
                    Notes = $"Extra {label} via Lemon Squeezy" + tag,
                });
                Log(tenant, "Billing", $"Extra {label} payment received", $"{Money(attrs)}{tag}");
                return true;
            }
            default:
                return false;
        }
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
                s => (s.PaymentProvider == ProviderName && s.PaymentProviderTokenRef == lemonSubscriptionId)
                    || s.ExtraBranchesLemonSubscriptionId == lemonSubscriptionId
                    || s.ExtraUsersLemonSubscriptionId == lemonSubscriptionId, ct);
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
