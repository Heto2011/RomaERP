using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public interface IBranchMinimumService
{
    Task<List<BranchMinimumDto>> GetAllAsync(CancellationToken ct = default);
    Task SetAsync(SetBranchMinimumDto dto, CancellationToken ct = default);
    Task<List<BranchShortageAlertDto>> GetAlertsAsync(CancellationToken ct = default);
}

public class BranchMinimumService : IBranchMinimumService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchAccessService? _access;

    public BranchMinimumService(IApplicationDbContext context, IBranchAccessService? access = null)
    {
        _context = context;
        _access = access;
    }

    public async Task<List<BranchMinimumDto>> GetAllAsync(CancellationToken ct = default)
    {
        var minimums = await _context.BranchItemMinimums.AsNoTracking().Include(m => m.Warehouse).Include(m => m.Item).Where(m => !m.IsDeleted).ToListAsync(ct);
        var balances = await BalancesAsync(ct);
        var restricted = _access is null ? null : await _access.GetRestrictedBranchAsync(ct);
        return minimums.Where(m => restricted is null || m.WarehouseId == restricted)
            .Select(m => new BranchMinimumDto
            {
                WarehouseId = m.WarehouseId, WarehouseName = m.Warehouse?.NameAr ?? string.Empty, ItemId = m.ItemId, ItemCode = m.Item?.Code ?? string.Empty,
                ItemName = m.Item?.NameAr ?? string.Empty, Unit = m.Item?.UnitOfMeasure ?? string.Empty, MinimumQuantity = m.MinimumQuantity,
                Quantity = balances.GetValueOrDefault((m.ItemId, m.WarehouseId))
            }).OrderBy(m => m.WarehouseName).ThenBy(m => m.ItemCode).ToList();
    }

    public async Task SetAsync(SetBranchMinimumDto dto, CancellationToken ct = default)
    {
        if (dto.MinimumQuantity < 0) throw new ValidationAppException("الحد الأدنى مش ممكن يكون بالسالب.");
        if (_access is not null) await _access.EnsureMayUseAsync(dto.WarehouseId, ct);
        if (!await _context.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct)) throw new NotFoundException(nameof(Warehouse), dto.WarehouseId);
        if (!await _context.Items.AnyAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)) throw new NotFoundException(nameof(Item), dto.ItemId);

        var existing = await _context.BranchItemMinimums.FirstOrDefaultAsync(m => m.WarehouseId == dto.WarehouseId && m.ItemId == dto.ItemId && !m.IsDeleted, ct);
        if (dto.MinimumQuantity == 0)
        {
            if (existing is not null) existing.IsDeleted = true;
        }
        else if (existing is null)
        {
            _context.BranchItemMinimums.Add(new BranchItemMinimum { WarehouseId = dto.WarehouseId, ItemId = dto.ItemId, MinimumQuantity = Math.Round(dto.MinimumQuantity, 4) });
        }
        else
        {
            existing.MinimumQuantity = Math.Round(dto.MinimumQuantity, 4);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<BranchShortageAlertDto>> GetAlertsAsync(CancellationToken ct = default)
    {
        var minimums = await _context.BranchItemMinimums.AsNoTracking().Include(m => m.Warehouse).Include(m => m.Item).Where(m => !m.IsDeleted).ToListAsync(ct);
        if (minimums.Count == 0) return new();
        var balances = await BalancesAsync(ct);
        var incoming = await IncomingAsync(ct);
        var minByKey = minimums.ToDictionary(m => (m.ItemId, m.WarehouseId), m => m.MinimumQuantity);
        var warehouseNames = await _context.Warehouses.AsNoTracking().Where(w => !w.IsDeleted && w.IsActive).ToDictionaryAsync(w => w.Id, w => w.NameAr, ct);
        var restricted = _access is null ? null : await _access.GetRestrictedBranchAsync(ct);

        var alerts = new List<BranchShortageAlertDto>();
        foreach (var m in minimums.Where(m => restricted is null || m.WarehouseId == restricted))
        {
            var have = balances.GetValueOrDefault((m.ItemId, m.WarehouseId));
            var coming = incoming.GetValueOrDefault((m.ItemId, m.WarehouseId));
            var shortage = m.MinimumQuantity - have - coming;
            if (shortage <= 0) continue;

            // Other branches can give what they hold above their own minimum (or above zero when they have none), the biggest surplus first.
            var suggestions = new List<TransferSuggestionDto>();
            var remaining = shortage;
            foreach (var other in warehouseNames.Where(w => w.Key != m.WarehouseId)
                         .Select(w => (Id: w.Key, Name: w.Value, Surplus: balances.GetValueOrDefault((m.ItemId, w.Key)) - minByKey.GetValueOrDefault((m.ItemId, w.Key))))
                         .Where(x => x.Surplus > 0).OrderByDescending(x => x.Surplus))
            {
                if (remaining <= 0) break;
                var take = Math.Min(other.Surplus, remaining);
                suggestions.Add(new TransferSuggestionDto { FromWarehouseId = other.Id, FromWarehouseName = other.Name, Quantity = Math.Round(take, 4) });
                remaining -= take;
            }

            alerts.Add(new BranchShortageAlertDto
            {
                WarehouseId = m.WarehouseId, WarehouseName = m.Warehouse?.NameAr ?? string.Empty, ItemId = m.ItemId, ItemCode = m.Item?.Code ?? string.Empty,
                ItemName = m.Item?.NameAr ?? string.Empty, Unit = m.Item?.UnitOfMeasure ?? string.Empty, MinimumQuantity = m.MinimumQuantity,
                Quantity = have, Incoming = coming, Shortage = Math.Round(shortage, 4), Suggestions = suggestions, ToBuy = Math.Round(Math.Max(0, remaining), 4)
            });
        }
        return alerts.OrderByDescending(a => a.Shortage / (a.MinimumQuantity == 0 ? 1 : a.MinimumQuantity)).ToList();
    }

    private async Task<Dictionary<(Guid ItemId, Guid WarehouseId), decimal>> BalancesAsync(CancellationToken ct)
    {
        var movements = await _context.StockMovements.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new { m.ItemId, m.WarehouseId, m.MovementType, m.Quantity }).ToListAsync(ct);
        return movements.GroupBy(m => (m.ItemId, m.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(m => m.MovementType is StockMovementType.Receipt or StockMovementType.TransferIn ? m.Quantity : -m.Quantity));
    }

    private async Task<Dictionary<(Guid ItemId, Guid WarehouseId), decimal>> IncomingAsync(CancellationToken ct)
    {
        var lines = await _context.StockTransfers.AsNoTracking().Where(t => !t.IsDeleted && t.Status == StockTransferStatus.InTransit)
            .SelectMany(t => t.Lines.Select(l => new { l.ItemId, t.ToWarehouseId, l.Quantity })).ToListAsync(ct);
        return lines.GroupBy(l => (l.ItemId, l.ToWarehouseId)).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
    }
}
