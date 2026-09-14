using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.DTOs;

public class DeliveryPlatformStatusDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
}

public class DeliveryWebhookEventDto
{
    public Guid Id { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string? ExternalOrderId { get; set; }
    public DeliveryWebhookEventStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSignatureVerified { get; set; }
    public Guid? CreatedOrderId { get; set; }
    public string? CreatedOrderNumber { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}

public class DeliveryPlatformItemMappingDto
{
    public Guid Id { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string ExternalItemId { get; set; } = string.Empty;
    public string? ExternalItemName { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
}

public class SaveDeliveryPlatformItemMappingDto
{
    public string PlatformName { get; set; } = string.Empty;
    public string ExternalItemId { get; set; } = string.Empty;
    public string? ExternalItemName { get; set; }
    public Guid ItemId { get; set; }
}
