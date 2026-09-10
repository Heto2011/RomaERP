using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

/// <summary>Per-batch quantity and expiry tracking, opt-in per item (Item.IsLotTracked). A no-op for items
/// that aren't lot-tracked, so every receipt/consumption call site can call it unconditionally. Purely a side
/// ledger for "what do I have and when does it expire" — it never changes how anything is costed.</summary>
public interface IItemLotService
{
    Task<List<ItemLotDto>> GetLotsAsync(CancellationToken ct = default);

    /// <summary>Adds to (or creates) the named lot. No-op if the item isn't lot-tracked; throws if it is
    /// lot-tracked but no lot number was given.</summary>
    Task ReceiveLotAsync(Guid itemId, Guid warehouseId, string? lotNumber, decimal quantity, decimal unitCost, DateTime? expiryDate, DateTime receivedDate, CancellationToken ct = default);

    /// <summary>Draws down the item's lots FEFO (earliest expiry first) by the given quantity. No-op if the
    /// item isn't lot-tracked. Does not itself validate available quantity — the caller's own stock check
    /// against Item.QuantityOnHand remains the source of truth.</summary>
    Task ConsumeFefoAsync(Guid itemId, Guid warehouseId, decimal quantity, CancellationToken ct = default);

    Task<List<ExpiringLotDto>> GetExpiringLotsAsync(int withinDays, CancellationToken ct = default);
}
