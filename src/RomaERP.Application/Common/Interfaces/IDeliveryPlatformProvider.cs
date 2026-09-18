namespace RomaERP.Application.Common.Interfaces;

public record DeliveryOrderItemPayload(string ExternalItemId, string? ExternalItemName, decimal Quantity, decimal UnitPrice);

public record DeliveryOrderPayload(
    string ExternalOrderId,
    string? CustomerName,
    string? CustomerPhone,
    string? DeliveryAddress,
    List<DeliveryOrderItemPayload> Items,
    DateTime PlacedAtUtc);

public record WebhookVerificationResult(bool IsValid, string? FailureReason);

/// <summary>One delivery-aggregator's webhook intake: verifies the platform actually sent the request
/// (shared-secret HMAC, the standard scheme these platforms use) and parses its order payload into our
/// normalized shape. Registered as one implementation per platform (HungerStation/Jahez/Mrsool) — the
/// intake service picks the right one by <see cref="Name"/>. The HMAC secret is supplied per call (looked
/// up per-tenant by the caller, from that tenant's own DeliveryPlatformCredential row) rather than read
/// from global configuration — each restaurant gets its own credential from that platform's own
/// POS-integration partner program, and a single shared secret would let a forged/replayed webhook signed
/// for one tenant's company code also verify against every other tenant using the same platform. None of
/// these three platforms publish a public, self-serve API spec — each requires enrolling in that
/// platform's own partner/POS-integration program to get real credentials and the exact webhook payload
/// shape — so <see cref="ParseOrderPayload"/>'s field mapping is a best-effort placeholder to adjust once
/// that real documentation is in hand; nothing else in the intake pipeline (matching, order creation,
/// billing, GL posting) needs to change.</summary>
public interface IDeliveryPlatformProvider
{
    string Name { get; }
    WebhookVerificationResult VerifySignature(string rawBody, string? signatureHeader, string secret);
    DeliveryOrderPayload ParseOrderPayload(string rawBody);
}
