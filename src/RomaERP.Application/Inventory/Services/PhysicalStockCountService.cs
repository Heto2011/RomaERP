using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class PhysicalStockCountService : IPhysicalStockCountService
{
    private readonly IApplicationDbContext _context;

    public PhysicalStockCountService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PhysicalStockCountDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.PhysicalStockCounts
            .AsNoTracking()
            .Include(c => c.Item)
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

        var count = new PhysicalStockCount
        {
            ItemId = item.Id,
            CountDate = dto.CountDate,
            SystemQuantity = item.QuantityOnHand,
            CountedQuantity = dto.CountedQuantity,
            UnitCost = item.AverageCost,
            Notes = dto.Notes?.Trim()
        };

        _context.PhysicalStockCounts.Add(count);
        await _context.SaveChangesAsync(ct);

        count.Item = item;
        return Map(count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var count = await _context.PhysicalStockCounts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(PhysicalStockCount), id);

        count.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
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
            Notes = c.Notes
        };
    }
}
