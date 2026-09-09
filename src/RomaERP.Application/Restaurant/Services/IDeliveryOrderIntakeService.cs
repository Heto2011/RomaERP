using RomaERP.Application.Restaurant.DTOs;

namespace RomaERP.Application.Restaurant.Services;

public interface IDeliveryOrderIntakeService
{
    List<DeliveryPlatformStatusDto> GetPlatformStatuses();
    Task<DeliveryWebhookEventDto> ReceiveWebhookAsync(string platformName, string rawBody, string? signatureHeader, CancellationToken ct = default);
    Task<DeliveryWebhookEventDto> RetryAsync(Guid webhookEventId, CancellationToken ct = default);
    Task<List<DeliveryWebhookEventDto>> GetEventsAsync(CancellationToken ct = default);

    Task<List<DeliveryPlatformItemMappingDto>> GetItemMappingsAsync(string? platformName, CancellationToken ct = default);
    Task<DeliveryPlatformItemMappingDto> SetItemMappingAsync(SaveDeliveryPlatformItemMappingDto dto, CancellationToken ct = default);
    Task DeleteItemMappingAsync(Guid id, CancellationToken ct = default);
}
