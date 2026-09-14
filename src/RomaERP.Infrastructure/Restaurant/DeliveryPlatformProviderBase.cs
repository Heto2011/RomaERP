using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Restaurant;

/// <summary>Shared HMAC-signature verification and JSON payload parsing for every delivery-platform
/// provider (see IDeliveryPlatformProvider's doc comment for why the payload shape here is a placeholder,
/// not a verified spec, until each platform's real partner-program docs are in hand). Inert
/// (<see cref="IsConfigured"/> false) until "&lt;PlatformConfigKey&gt;:WebhookSecret" is set.</summary>
public abstract class DeliveryPlatformProviderBase : IDeliveryPlatformProvider
{
    private readonly string? _webhookSecret;

    protected DeliveryPlatformProviderBase(IConfiguration configuration, string platformConfigKey, string name)
    {
        _webhookSecret = configuration[$"{platformConfigKey}:WebhookSecret"];
        Name = name;
    }

    public string Name { get; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_webhookSecret);

    public WebhookVerificationResult VerifySignature(string rawBody, string? signatureHeader)
    {
        if (!IsConfigured)
            return new WebhookVerificationResult(false, $"{Name} غير مفعّل — لسه مفيش WebhookSecret متظبط في الإعدادات.");

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return new WebhookVerificationResult(false, "الطلب وصل من غير توقيع (signature header).");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret!));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var provided = signatureHeader.Trim().ToLowerInvariant();

        var isValid = computed.Length == provided.Length
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(provided));

        return isValid
            ? new WebhookVerificationResult(true, null)
            : new WebhookVerificationResult(false, "توقيع الطلب غير صحيح — الطلب مش مؤكد إنه جاي من المنصة فعلاً.");
    }

    /// <summary>Placeholder generic contract (order_id, customer_name/phone, delivery_address, placed_at,
    /// items[{item_id, item_name, quantity, unit_price}]) — override in a subclass once that platform's
    /// real webhook payload shape is known.</summary>
    public virtual DeliveryOrderPayload ParseOrderPayload(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var items = new List<DeliveryOrderItemPayload>();
        if (root.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsEl.EnumerateArray())
            {
                items.Add(new DeliveryOrderItemPayload(
                    ExternalItemId: item.TryGetProperty("item_id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    ExternalItemName: item.TryGetProperty("item_name", out var n) ? n.GetString() : null,
                    Quantity: item.TryGetProperty("quantity", out var q) ? q.GetDecimal() : 1,
                    UnitPrice: item.TryGetProperty("unit_price", out var p) ? p.GetDecimal() : 0));
            }
        }

        return new DeliveryOrderPayload(
            ExternalOrderId: root.TryGetProperty("order_id", out var oid) ? oid.GetString() ?? string.Empty : string.Empty,
            CustomerName: root.TryGetProperty("customer_name", out var cn) ? cn.GetString() : null,
            CustomerPhone: root.TryGetProperty("customer_phone", out var cp) ? cp.GetString() : null,
            DeliveryAddress: root.TryGetProperty("delivery_address", out var da) ? da.GetString() : null,
            Items: items,
            PlacedAtUtc: root.TryGetProperty("placed_at", out var pa) && pa.TryGetDateTime(out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow);
    }
}
