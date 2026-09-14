using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

/// <summary>Turns a verified delivery-platform webhook into a real, billed RestaurantOrder by reusing
/// IRestaurantService's own CreateOrderAsync/AddLineAsync/BillOrderAsync — no GL or stock logic is
/// duplicated here. Every inbound call is logged as a DeliveryWebhookEvent whether it succeeds or not, so
/// a failure (bad signature, an unmapped menu item, no open fiscal period…) stays visible and retryable
/// instead of silently dropping an order.</summary>
public class DeliveryOrderIntakeService : IDeliveryOrderIntakeService
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IDeliveryPlatformProvider> _providers;
    private readonly IRestaurantService _restaurantService;

    public DeliveryOrderIntakeService(IApplicationDbContext context, IEnumerable<IDeliveryPlatformProvider> providers, IRestaurantService restaurantService)
    {
        _context = context;
        _providers = providers;
        _restaurantService = restaurantService;
    }

    public List<DeliveryPlatformStatusDto> GetPlatformStatuses()
        => _providers.Select(p => new DeliveryPlatformStatusDto { Name = p.Name, IsConfigured = p.IsConfigured }).ToList();

    public async Task<DeliveryWebhookEventDto> ReceiveWebhookAsync(string platformName, string rawBody, string? signatureHeader, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.Name, platformName, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Delivery platform", platformName);

        var webhookEvent = new DeliveryWebhookEvent
        {
            PlatformName = provider.Name,
            RawPayload = rawBody,
            Status = DeliveryWebhookEventStatus.Received,
            ReceivedAtUtc = DateTime.UtcNow
        };
        _context.DeliveryWebhookEvents.Add(webhookEvent);
        await _context.SaveChangesAsync(ct);

        var verification = provider.VerifySignature(rawBody, signatureHeader);
        if (!verification.IsValid)
        {
            Fail(webhookEvent, verification.FailureReason ?? "توقيع غير صحيح.");
            await _context.SaveChangesAsync(ct);
            return Map(webhookEvent);
        }

        webhookEvent.IsSignatureVerified = true;
        await ProcessAsync(webhookEvent, provider, ct);
        return Map(webhookEvent);
    }

    public async Task<DeliveryWebhookEventDto> RetryAsync(Guid webhookEventId, CancellationToken ct = default)
    {
        var webhookEvent = await _context.DeliveryWebhookEvents.Include(e => e.CreatedOrder).FirstOrDefaultAsync(e => e.Id == webhookEventId, ct)
            ?? throw new NotFoundException(nameof(DeliveryWebhookEvent), webhookEventId);

        if (webhookEvent.Status != DeliveryWebhookEventStatus.Failed)
            throw new ValidationAppException("مينفعش تعيد المحاولة إلا لحدث فشل.");

        if (!webhookEvent.IsSignatureVerified)
            throw new ValidationAppException("التوقيع الأصلي لهذا الطلب مش متأكد منه — لازم المنصة تبعت الطلب تاني بدل إعادة المحاولة.");

        var provider = _providers.FirstOrDefault(p => string.Equals(p.Name, webhookEvent.PlatformName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationAppException($"مزود التوصيل '{webhookEvent.PlatformName}' غير معروف.");

        await ProcessAsync(webhookEvent, provider, ct);
        return Map(webhookEvent);
    }

    public async Task<List<DeliveryWebhookEventDto>> GetEventsAsync(CancellationToken ct = default)
    {
        var events = await _context.DeliveryWebhookEvents
            .AsNoTracking()
            .Include(e => e.CreatedOrder)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Take(200)
            .ToListAsync(ct);

        return events.Select(Map).ToList();
    }

    public async Task<List<DeliveryPlatformItemMappingDto>> GetItemMappingsAsync(string? platformName, CancellationToken ct = default)
    {
        var query = _context.DeliveryPlatformItemMappings.AsNoTracking().Include(m => m.Item).AsQueryable();
        if (!string.IsNullOrWhiteSpace(platformName))
            query = query.Where(m => m.PlatformName == platformName);

        var mappings = await query.OrderBy(m => m.PlatformName).ThenBy(m => m.ExternalItemId).ToListAsync(ct);
        return mappings.Select(MapMapping).ToList();
    }

    public async Task<DeliveryPlatformItemMappingDto> SetItemMappingAsync(SaveDeliveryPlatformItemMappingDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.PlatformName) || string.IsNullOrWhiteSpace(dto.ExternalItemId))
            throw new ValidationAppException("اسم المنصة وكود الصنف عند المنصة مطلوبين.");

        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), dto.ItemId);

        var mapping = await _context.DeliveryPlatformItemMappings
            .FirstOrDefaultAsync(m => m.PlatformName == dto.PlatformName && m.ExternalItemId == dto.ExternalItemId, ct);

        if (mapping is null)
        {
            mapping = new DeliveryPlatformItemMapping { PlatformName = dto.PlatformName, ExternalItemId = dto.ExternalItemId };
            _context.DeliveryPlatformItemMappings.Add(mapping);
        }

        mapping.ExternalItemName = dto.ExternalItemName;
        mapping.ItemId = dto.ItemId;
        await _context.SaveChangesAsync(ct);

        return new DeliveryPlatformItemMappingDto
        {
            Id = mapping.Id,
            PlatformName = mapping.PlatformName,
            ExternalItemId = mapping.ExternalItemId,
            ExternalItemName = mapping.ExternalItemName,
            ItemId = mapping.ItemId,
            ItemName = item.NameAr
        };
    }

    public async Task DeleteItemMappingAsync(Guid id, CancellationToken ct = default)
    {
        var mapping = await _context.DeliveryPlatformItemMappings.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(DeliveryPlatformItemMapping), id);

        mapping.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private async Task ProcessAsync(DeliveryWebhookEvent webhookEvent, IDeliveryPlatformProvider provider, CancellationToken ct)
    {
        try
        {
            var payload = provider.ParseOrderPayload(webhookEvent.RawPayload);
            webhookEvent.ExternalOrderId = payload.ExternalOrderId;

            if (string.IsNullOrWhiteSpace(payload.ExternalOrderId))
            {
                Fail(webhookEvent, "الطلب وصل من غير رقم طلب (order_id).");
                await _context.SaveChangesAsync(ct);
                return;
            }

            var existingOrder = await _context.RestaurantOrders
                .FirstOrDefaultAsync(o => o.SourcePlatform == provider.Name && o.ExternalOrderRef == payload.ExternalOrderId, ct);
            if (existingOrder is not null)
            {
                webhookEvent.Status = DeliveryWebhookEventStatus.Processed;
                webhookEvent.CreatedOrderId = existingOrder.Id;
                webhookEvent.ErrorMessage = null;
                webhookEvent.ProcessedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return;
            }

            if (payload.Items.Count == 0)
            {
                Fail(webhookEvent, "الطلب وصل من غير أي أصناف.");
                await _context.SaveChangesAsync(ct);
                return;
            }

            var mappings = await _context.DeliveryPlatformItemMappings
                .Where(m => m.PlatformName == provider.Name)
                .ToListAsync(ct);

            var resolvedLines = new List<(Guid ItemId, decimal Quantity)>();
            var unmapped = new List<string>();
            foreach (var item in payload.Items)
            {
                var mapping = mappings.FirstOrDefault(m => m.ExternalItemId == item.ExternalItemId);
                if (mapping is null)
                {
                    unmapped.Add(item.ExternalItemName is { Length: > 0 } n ? $"{item.ExternalItemId} ({n})" : item.ExternalItemId);
                    continue;
                }
                resolvedLines.Add((mapping.ItemId, item.Quantity));
            }

            if (unmapped.Count > 0)
            {
                Fail(webhookEvent, $"أصناف مش مربوطة بعد لمنصة {provider.Name}: {string.Join("، ", unmapped)} — اربطها من صفحة \"منصات التوصيل\" وأعد المحاولة.");
                await _context.SaveChangesAsync(ct);
                return;
            }

            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, ct);
            if (warehouse is null)
            {
                Fail(webhookEvent, "مفيش مخزن نشط لاستقبال الطلب.");
                await _context.SaveChangesAsync(ct);
                return;
            }

            var fiscalPeriod = await _context.FiscalPeriods.Where(p => !p.IsClosed).OrderBy(p => p.StartDate).FirstOrDefaultAsync(ct);
            if (fiscalPeriod is null)
            {
                Fail(webhookEvent, "مفيش فترة محاسبية مفتوحة لترحيل الطلب.");
                await _context.SaveChangesAsync(ct);
                return;
            }

            var order = await _restaurantService.CreateOrderAsync(new CreateRestaurantOrderDto
            {
                OrderType = RestaurantOrderType.Delivery,
                CustomerName = payload.CustomerName,
                CustomerPhone = payload.CustomerPhone,
                DeliveryAddress = payload.DeliveryAddress,
                WarehouseId = warehouse.Id,
                Notes = $"طلب أوتوماتيكي من {provider.Name} - #{payload.ExternalOrderId}",
                SourcePlatform = provider.Name,
                ExternalOrderRef = payload.ExternalOrderId
            }, ct);

            foreach (var line in resolvedLines)
                await _restaurantService.AddLineAsync(order.Id, new AddOrderLineDto { ItemId = line.ItemId, Quantity = line.Quantity }, ct);

            var billedOrder = await _restaurantService.BillOrderAsync(order.Id, new BillOrderDto
            {
                PaymentTerm = PaymentTerm.Credit,
                FiscalPeriodId = fiscalPeriod.Id,
                DeliveryPlatformName = provider.Name
            }, ct);

            webhookEvent.Status = DeliveryWebhookEventStatus.Processed;
            webhookEvent.CreatedOrderId = billedOrder.Id;
            webhookEvent.ErrorMessage = null;
            webhookEvent.ProcessedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Fail(webhookEvent, ex.Message);
            await _context.SaveChangesAsync(ct);
        }
    }

    private static void Fail(DeliveryWebhookEvent webhookEvent, string message)
    {
        webhookEvent.Status = DeliveryWebhookEventStatus.Failed;
        webhookEvent.ErrorMessage = message;
        webhookEvent.ProcessedAtUtc = DateTime.UtcNow;
    }

    private static DeliveryWebhookEventDto Map(DeliveryWebhookEvent e) => new()
    {
        Id = e.Id,
        PlatformName = e.PlatformName,
        ExternalOrderId = e.ExternalOrderId,
        Status = e.Status,
        ErrorMessage = e.ErrorMessage,
        IsSignatureVerified = e.IsSignatureVerified,
        CreatedOrderId = e.CreatedOrderId,
        CreatedOrderNumber = e.CreatedOrder?.OrderNumber,
        ReceivedAtUtc = e.ReceivedAtUtc,
        ProcessedAtUtc = e.ProcessedAtUtc
    };

    private static DeliveryPlatformItemMappingDto MapMapping(DeliveryPlatformItemMapping m) => new()
    {
        Id = m.Id,
        PlatformName = m.PlatformName,
        ExternalItemId = m.ExternalItemId,
        ExternalItemName = m.ExternalItemName,
        ItemId = m.ItemId,
        ItemName = m.Item?.NameAr ?? string.Empty
    };
}
