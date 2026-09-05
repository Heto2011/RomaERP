using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class WasteEntryService : IWasteEntryService
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;

    public WasteEntryService(IApplicationDbContext context, IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    public async Task<List<WasteEntryDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.WasteEntries
            .AsNoTracking()
            .Include(w => w.Item)
            .OrderByDescending(w => w.WasteDate)
            .Select(w => Map(w))
            .ToListAsync(ct);
    }

    public async Task<WasteEntryDto> CreateAsync(CreateWasteEntryDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0)
            throw new ValidationAppException("الكمية يجب أن تكون أكبر من صفر.");

        var reasonText = ((WasteReason)dto.Reason).ToString();

        var movement = await _inventoryService.IssueStockAsync(new IssueStockDto
        {
            MovementDate = dto.WasteDate,
            FiscalPeriodId = dto.FiscalPeriodId,
            ItemId = dto.ItemId,
            WarehouseId = dto.WarehouseId,
            Quantity = dto.Quantity,
            Reference = "WASTE",
            Description = $"هالك ({reasonText})" + (string.IsNullOrWhiteSpace(dto.Notes) ? "" : $" - {dto.Notes}")
        }, ct);

        var entry = new WasteEntry
        {
            ItemId = dto.ItemId,
            WasteDate = dto.WasteDate,
            Quantity = dto.Quantity,
            UnitCost = movement.UnitCost,
            TotalCost = movement.TotalCost,
            Reason = (WasteReason)dto.Reason,
            Notes = dto.Notes?.Trim(),
            StockMovementId = movement.Id
        };

        _context.WasteEntries.Add(entry);
        await _context.SaveChangesAsync(ct);

        entry.Item = await _context.Items.FirstAsync(i => i.Id == dto.ItemId, ct);
        return Map(entry);
    }

    private static WasteEntryDto Map(WasteEntry w) => new()
    {
        Id = w.Id,
        ItemId = w.ItemId,
        ItemCode = w.Item!.Code,
        ItemName = w.Item.NameAr,
        WasteDate = w.WasteDate,
        Quantity = w.Quantity,
        UnitCost = w.UnitCost,
        TotalCost = w.TotalCost,
        Reason = (int)w.Reason,
        Notes = w.Notes
    };
}
