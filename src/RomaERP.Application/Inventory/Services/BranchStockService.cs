using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public interface IBranchStockService
{
    Task<bool> IsEnforcedAsync(CancellationToken ct = default);
    Task SetEnforcedAsync(bool enforced, CancellationToken ct = default);
    /// <summary>What one branch holds of one item (its receipts and transfers in, minus its issues and transfers out).</summary>
    Task<decimal> GetBalanceAsync(Guid itemId, Guid warehouseId, CancellationToken ct = default);
    /// <summary>When the company turned on branch stock checks, refuses a quantity larger than the branch holds. Otherwise does nothing.</summary>
    Task EnsureAvailableAsync(Item item, Guid warehouseId, decimal quantity, CancellationToken ct = default);
}

public class BranchStockService : IBranchStockService
{
    private readonly IApplicationDbContext _context;

    public BranchStockService(IApplicationDbContext context) => _context = context;

    public async Task<bool> IsEnforcedAsync(CancellationToken ct = default)
        => (await _context.CompanySettings.AsNoTracking().Select(s => (bool?)s.EnforceBranchStock).FirstOrDefaultAsync(ct)) ?? false;

    public async Task SetEnforcedAsync(bool enforced, CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings.FirstOrDefaultAsync(ct)
            ?? throw new ValidationAppException("إعدادات الشركة مش موجودة.");
        settings.EnforceBranchStock = enforced;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(Guid itemId, Guid warehouseId, CancellationToken ct = default)
    {
        var movements = await _context.StockMovements.AsNoTracking()
            .Where(m => m.ItemId == itemId && m.WarehouseId == warehouseId)
            .Select(m => new { m.MovementType, m.Quantity }).ToListAsync(ct);
        return movements.Sum(m => m.MovementType is StockMovementType.Receipt or StockMovementType.TransferIn ? m.Quantity : -m.Quantity);
    }

    public async Task EnsureAvailableAsync(Item item, Guid warehouseId, decimal quantity, CancellationToken ct = default)
    {
        if (!await IsEnforcedAsync(ct)) return;
        var balance = await GetBalanceAsync(item.Id, warehouseId, ct);
        if (quantity <= balance) return;
        var branch = await _context.Warehouses.AsNoTracking().Where(w => w.Id == warehouseId).Select(w => w.NameAr).FirstOrDefaultAsync(ct) ?? "الفرع";
        throw new ValidationAppException(
            $"رصيد {item.Code} - {item.NameAr} في {branch} هو {balance:0.####} والمطلوب {quantity:0.####}. حوّل كمية من فرع تاني أو سجّل استلام.");
    }
}
