using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class ItemLotService : IItemLotService
{
    private readonly IApplicationDbContext _context;

    public ItemLotService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ItemLotDto>> GetLotsAsync(CancellationToken ct = default)
    {
        var lots = await _context.ItemLots
            .AsNoTracking()
            .Include(l => l.Item)
            .Include(l => l.Warehouse)
            .Where(l => l.QuantityOnHand > 0 && !l.IsDeleted)
            .OrderBy(l => l.ExpiryDate ?? DateTime.MaxValue)
            .ToListAsync(ct);

        return lots.Select(Map).ToList();
    }

    public async Task ReceiveLotAsync(Guid itemId, Guid warehouseId, string? lotNumber, decimal quantity, decimal unitCost, DateTime? expiryDate, DateTime receivedDate, CancellationToken ct = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted, ct);
        if (item is null || !item.IsLotTracked) return;

        if (string.IsNullOrWhiteSpace(lotNumber))
            throw new ValidationAppException($"الصنف {item.Code} بيتتبع بالدُفعات — لازم تحدد رقم الدُفعة.");

        var lot = await _context.ItemLots.FirstOrDefaultAsync(
            l => l.ItemId == itemId && l.WarehouseId == warehouseId && l.LotNumber == lotNumber && !l.IsDeleted, ct);

        if (lot is null)
        {
            _context.ItemLots.Add(new ItemLot
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                LotNumber = lotNumber.Trim(),
                QuantityOnHand = quantity,
                UnitCost = unitCost,
                ExpiryDate = expiryDate,
                ReceivedDate = receivedDate,
            });
            return;
        }

        var newQuantity = lot.QuantityOnHand + quantity;
        lot.UnitCost = newQuantity == 0 ? unitCost : Math.Round(((lot.QuantityOnHand * lot.UnitCost) + (quantity * unitCost)) / newQuantity, 4);
        lot.QuantityOnHand = newQuantity;
        if (expiryDate.HasValue) lot.ExpiryDate = expiryDate;
    }

    public async Task ConsumeFefoAsync(Guid itemId, Guid warehouseId, decimal quantity, CancellationToken ct = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted, ct);
        if (item is null || !item.IsLotTracked || quantity <= 0) return;

        var lots = await _context.ItemLots
            .Where(l => l.ItemId == itemId && l.WarehouseId == warehouseId && l.QuantityOnHand > 0 && !l.IsDeleted)
            .OrderBy(l => l.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(l => l.ReceivedDate)
            .ToListAsync(ct);

        var remaining = quantity;
        foreach (var lot in lots)
        {
            if (remaining <= 0) break;
            var take = Math.Min(lot.QuantityOnHand, remaining);
            lot.QuantityOnHand -= take;
            remaining -= take;
        }
        // If lots ran out before `remaining` reached zero, the lot ledger fell out of sync with the item's
        // aggregate QuantityOnHand (e.g. a receipt was posted without a lot number). Nothing further to
        // subtract here — the aggregate remains the source of truth for stock availability everywhere else.
    }

    public async Task<List<ExpiringLotDto>> GetExpiringLotsAsync(int withinDays, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var threshold = today.AddDays(withinDays);

        var lots = await _context.ItemLots
            .AsNoTracking()
            .Include(l => l.Item)
            .Include(l => l.Warehouse)
            .Where(l => l.QuantityOnHand > 0 && l.ExpiryDate != null && l.ExpiryDate <= threshold && !l.IsDeleted)
            .OrderBy(l => l.ExpiryDate)
            .ToListAsync(ct);

        return lots.Select(l => new ExpiringLotDto
        {
            ItemId = l.ItemId,
            ItemCode = l.Item?.Code ?? string.Empty,
            ItemName = l.Item?.NameAr ?? string.Empty,
            WarehouseName = l.Warehouse?.NameAr ?? string.Empty,
            LotNumber = l.LotNumber,
            QuantityOnHand = l.QuantityOnHand,
            UnitCost = l.UnitCost,
            ValueAtRisk = Math.Round(l.QuantityOnHand * l.UnitCost, 2),
            ExpiryDate = l.ExpiryDate!.Value,
            IsExpired = l.ExpiryDate.Value.Date < today,
            DaysUntilExpiry = (l.ExpiryDate.Value.Date - today).Days
        }).ToList();
    }

    private static ItemLotDto Map(ItemLot l) => new()
    {
        Id = l.Id,
        ItemId = l.ItemId,
        ItemCode = l.Item?.Code ?? string.Empty,
        ItemName = l.Item?.NameAr ?? string.Empty,
        WarehouseId = l.WarehouseId,
        WarehouseName = l.Warehouse?.NameAr ?? string.Empty,
        LotNumber = l.LotNumber,
        QuantityOnHand = l.QuantityOnHand,
        UnitCost = l.UnitCost,
        ExpiryDate = l.ExpiryDate,
        ReceivedDate = l.ReceivedDate,
    };
}
