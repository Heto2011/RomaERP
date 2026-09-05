using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Domain.Inventory;

namespace RomaERP.Application.Inventory.Services;

public class ItemCategoryService : IItemCategoryService
{
    private readonly IApplicationDbContext _context;

    public ItemCategoryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ItemCategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ItemCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Code)
            .Select(c => new ItemCategoryDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, IsActive = c.IsActive })
            .ToListAsync(ct);
    }

    public async Task<ItemCategoryDto> CreateAsync(CreateItemCategoryDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.ItemCategories.AnyAsync(c => c.Code == dto.Code && !c.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود التصنيف '{dto.Code}' مستخدم بالفعل.");

        var category = new ItemCategory { Code = dto.Code.Trim(), NameAr = dto.NameAr.Trim(), NameEn = dto.NameEn.Trim(), IsActive = true };
        _context.ItemCategories.Add(category);
        await _context.SaveChangesAsync(ct);

        return new ItemCategoryDto { Id = category.Id, Code = category.Code, NameAr = category.NameAr, NameEn = category.NameEn, IsActive = category.IsActive };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _context.ItemCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(ItemCategory), id);

        var hasItems = await _context.Items.AnyAsync(i => i.ItemCategoryId == id && !i.IsDeleted, ct);
        if (hasItems)
            throw new ValidationAppException("لا يمكن حذف تصنيف له أصناف مرتبطة.");

        category.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }
}

public class WarehouseService : IWarehouseService
{
    private readonly IApplicationDbContext _context;

    public WarehouseService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WarehouseDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .Where(w => !w.IsDeleted)
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseDto { Id = w.Id, Code = w.Code, NameAr = w.NameAr, NameEn = w.NameEn, IsActive = w.IsActive })
            .ToListAsync(ct);
    }

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.Warehouses.AnyAsync(w => w.Code == dto.Code && !w.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود المخزن '{dto.Code}' مستخدم بالفعل.");

        var warehouse = new Warehouse { Code = dto.Code.Trim(), NameAr = dto.NameAr.Trim(), NameEn = dto.NameEn.Trim(), IsActive = true };
        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync(ct);

        return new WarehouseDto { Id = warehouse.Id, Code = warehouse.Code, NameAr = warehouse.NameAr, NameEn = warehouse.NameEn, IsActive = warehouse.IsActive };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        var hasMovements = await _context.StockMovements.AnyAsync(m => m.WarehouseId == id, ct);
        if (hasMovements)
            throw new ValidationAppException("لا يمكن حذف مخزن له حركات مخزون.");

        warehouse.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }
}

public class ItemService : IItemService
{
    private readonly IApplicationDbContext _context;

    public ItemService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _context.Items
            .AsNoTracking()
            .Include(i => i.ItemCategory)
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Code)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<ItemDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.Items
            .AsNoTracking()
            .Include(i => i.ItemCategory)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException(nameof(Item), id);

        return Map(item);
    }

    public async Task<ItemDto> CreateAsync(CreateItemDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.Items.AnyAsync(i => i.Code == dto.Code && !i.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود الصنف '{dto.Code}' مستخدم بالفعل.");

        var categoryExists = await _context.ItemCategories.AnyAsync(c => c.Id == dto.ItemCategoryId && !c.IsDeleted, ct);
        if (!categoryExists)
            throw new NotFoundException(nameof(ItemCategory), dto.ItemCategoryId);

        var item = new Item
        {
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            UnitOfMeasure = dto.UnitOfMeasure.Trim(),
            ItemCategoryId = dto.ItemCategoryId,
            ReorderLevel = dto.ReorderLevel,
            IsActive = true,
            QuantityOnHand = 0,
            AverageCost = 0
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(item.Id, ct);
    }

    public async Task<ItemDto> UpdateAsync(Guid id, UpdateItemDto dto, CancellationToken ct = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Item), id);

        var categoryExists = await _context.ItemCategories.AnyAsync(c => c.Id == dto.ItemCategoryId && !c.IsDeleted, ct);
        if (!categoryExists)
            throw new NotFoundException(nameof(ItemCategory), dto.ItemCategoryId);

        item.NameAr = dto.NameAr.Trim();
        item.NameEn = dto.NameEn.Trim();
        item.UnitOfMeasure = dto.UnitOfMeasure.Trim();
        item.ItemCategoryId = dto.ItemCategoryId;
        item.ReorderLevel = dto.ReorderLevel;

        await _context.SaveChangesAsync(ct);
        return await GetByIdAsync(item.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException(nameof(Item), id);

        var hasMovements = await _context.StockMovements.AnyAsync(m => m.ItemId == id, ct);
        if (hasMovements)
            throw new ValidationAppException("لا يمكن حذف صنف له حركات مخزون.");

        item.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static ItemDto Map(Item i) => new()
    {
        Id = i.Id,
        Code = i.Code,
        NameAr = i.NameAr,
        NameEn = i.NameEn,
        UnitOfMeasure = i.UnitOfMeasure,
        ItemCategoryId = i.ItemCategoryId,
        ItemCategoryName = i.ItemCategory?.NameAr ?? string.Empty,
        ReorderLevel = i.ReorderLevel,
        QuantityOnHand = i.QuantityOnHand,
        AverageCost = i.AverageCost,
        IsActive = i.IsActive,
        IsMenuItem = i.IsMenuItem,
        MenuPrice = i.MenuPrice
    };
}
