using RomaERP.Domain.Common;

namespace RomaERP.Domain.Restaurant;

public enum DeliveryWebhookEventStatus
{
    Received = 1,
    Processed = 2,
    Failed = 3
}

/// <summary>An audit record of one inbound delivery-platform webhook call — kept whether it succeeded or
/// not, so a failed intake (bad signature, unmapped menu item, closed fiscal period…) is visible to staff
/// and can be fixed and retried (see DeliveryOrderIntakeService.RetryAsync) without losing the order data
/// the platform sent.</summary>
public class DeliveryWebhookEvent : AuditableEntity
{
    public string PlatformName { get; set; } = string.Empty;
    public string? ExternalOrderId { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public DeliveryWebhookEventStatus Status { get; set; } = DeliveryWebhookEventStatus.Received;
    public string? ErrorMessage { get; set; }

    /// <summary>True once the platform's signature on this exact payload has been checked and passed —
    /// RetryAsync requires this (retrying never re-checks the signature, since the original signature
    /// header isn't stored), so a request that failed signature verification can only come back in as a
    /// fresh webhook call, never be waved through by retrying.</summary>
    public bool IsSignatureVerified { get; set; }

    public Guid? CreatedOrderId { get; set; }
    public RestaurantOrder? CreatedOrder { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
