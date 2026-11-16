using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class PhysicalStockCountService : IPhysicalStockCountService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchStockService? _branchStock;
    private readonly IStockAdjustmentService? _adjustments;

    public PhysicalStockCountService(IApplicationDbContext context, IBranchStockService? branchStock = null, IStockAdjustmentService? adjustments = null)
    {
        _context = context;
        _branchStock = branchStock;
        _adjustments = adjustments;
    }

    public async Task<List<PhysicalStockCountDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.PhysicalStockCounts
            .AsNoTracking()
            .Include(c => c.Item).Include(c => c.Warehouse)
            .OrderByDescending(c => c.CountDate)
            .Select(c => Map(c))
            .ToListAsync(ct);
    }

    public async Task<PhysicalStockCountDto> CreateAsync(CreatePhysicalStockCountDto dto, CancellationToken ct = default)
    {
        if (dto.CountedQuantity < 0)
            throw new ValidationAppException("الكمية المعدودة لا يمكن أن تكون سالبة.");

        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == dto.ItemId && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), dto.ItemId);

        decimal systemQuantity = item.QuantityOnHand;
        Warehouse? warehouse = null;
        if (dto.WarehouseId is { } wid)
        {
            warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wid && !w.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Warehouse), wid);
            if (_branchStock is not null) systemQuantity = await _branchStock.GetBalanceAsync(item.Id, wid, ct);
        }

        var count = new PhysicalStockCount
        {
            ItemId = item.Id,
            WarehouseId = dto.WarehouseId,
            CountDate = dto.CountDate,
            SystemQuantity = systemQuantity,
            CountedQuantity = dto.CountedQuantity,
            UnitCost = item.AverageCost,
            Notes = dto.Notes?.Trim()
        };

        _context.PhysicalStockCounts.Add(count);
        await _context.SaveChangesAsync(ct);

        count.Item = item;
        var result = Map(count);
        result.WarehouseName = warehouse?.NameAr;
        return result;
    }

    /// <summary>Turns the count's difference into a stock adjustment (up when more was found, down when less), so the books and the
    /// branch balance match what was counted.</summary>
    public async Task<PhysicalStockCountDto> ApplyAsync(Guid id, string? userName, CancellationToken ct = default)
    {
        if (_adjustments is null) throw new ValidationAppException("التسوية مش متاحة.");
        var count = await _context.PhysicalStockCounts.Include(c => c.Item).Include(c => c.Warehouse).FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(PhysicalStockCount), id);
        if (count.StockAdjustmentId is not null) throw new ValidationAppException("الجرد ده اتحوّل لتسوية قبل كده.");
        if (count.WarehouseId is null) throw new ValidationAppException("الجرد القديم مش مربوط بفرع. اعمل جرد جديد لفرع معيّن.");
        var variance = count.CountedQuantity - count.SystemQuantity;
        if (variance == 0) throw new ValidationAppException("مفيش فرق في الجرد.");

        var adj = await _adjustments.CreateAsync(new CreateStockAdjustmentDto
        {
            WarehouseId = count.WarehouseId.Value, ItemId = count.ItemId, IsIncrease = variance > 0, Quantity = Math.Abs(variance),
            UnitCost = variance > 0 ? count.UnitCost : null, Reason = variance > 0 ? (int)StockAdjustmentReason.CountGain : (int)StockAdjustmentReason.CountLoss,
            AdjustmentDate = count.CountDate, Notes = string.IsNullOrWhiteSpace(count.Notes) ? "من جرد فعلي" : $"من جرد فعلي - {count.Notes}"
        }, userName, ct);
        count.StockAdjustmentId = adj.Id;
        await _context.SaveChangesAsync(ct);
        return Map(count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var count = await _context.PhysicalStockCounts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(PhysicalStockCount), id);

        count.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<CountVarianceRowDto>> GetVarianceReportAsync(DateTime? from, DateTime? to, Guid? warehouseId, CancellationToken ct = default)
    {
        var q = _context.PhysicalStockCounts.AsNoTracking().Include(c => c.Item).Include(c => c.Warehouse).Where(c => !c.IsDeleted);
        if (from is { } f) q = q.Where(c => c.CountDate >= f.Date);
        if (to is { } t) q = q.Where(c => c.CountDate <= t.Date);
        if (warehouseId is { } w) q = q.Where(c => c.WarehouseId == w);
        var counts = await q.ToListAsync(ct);
        return counts.GroupBy(c => new { c.WarehouseId, c.ItemId }).Select(g =>
        {
            var vars = g.Select(c => (Qty: c.CountedQuantity - c.SystemQuantity, Value: (c.CountedQuantity - c.SystemQuantity) * c.UnitCost)).ToList();
            var first = g.First();
            return new CountVarianceRowDto
            {
                WarehouseId = g.Key.WarehouseId, WarehouseName = first.Warehouse?.NameAr ?? string.Empty, ItemId = g.Key.ItemId,
                ItemCode = first.Item!.Code, ItemName = first.Item.NameAr, Counts = vars.Count,
                ShortQuantity = -vars.Where(v => v.Qty < 0).Sum(v => v.Qty), OverQuantity = vars.Where(v => v.Qty > 0).Sum(v => v.Qty),
                NetQuantity = vars.Sum(v => v.Qty), NetValue = Math.Round(vars.Sum(v => v.Value), 2)
            };
        }).OrderBy(r => r.NetValue).ToList();
    }

    private static PhysicalStockCountDto Map(PhysicalStockCount c)
    {
        var variance = c.CountedQuantity - c.SystemQuantity;
        return new PhysicalStockCountDto
        {
            Id = c.Id,
            ItemId = c.ItemId,
            ItemCode = c.Item!.Code,
            ItemName = c.Item.NameAr,
            CountDate = c.CountDate,
            SystemQuantity = c.SystemQuantity,
            CountedQuantity = c.CountedQuantity,
            Variance = variance,
            UnitCost = c.UnitCost,
            VarianceValue = Math.Round(variance * c.UnitCost, 2),
            Notes = c.Notes,
            WarehouseId = c.WarehouseId,
            WarehouseName = c.Warehouse?.NameAr,
            StockAdjustmentId = c.StockAdjustmentId
        };
    }
}
