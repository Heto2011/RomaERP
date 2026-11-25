using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

public interface IItemCategoryService
{
    Task<List<ItemCategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<ItemCategoryDto> CreateAsync(CreateItemCategoryDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IWarehouseService
{
    Task<List<WarehouseDto>> GetAllAsync(CancellationToken ct = default);
    Task<WarehouseDto> CreateAsync(CreateWarehouseDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IItemService
{
    Task<List<ItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<ItemDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Finds an active item by the scanned code: the barcode first, then the item code (people also type that).</summary>
    Task<ItemDto> GetByBarcodeAsync(string code, CancellationToken ct = default);
    Task<ItemDto> CreateAsync(CreateItemDto dto, CancellationToken ct = default);
    Task<ItemDto> UpdateAsync(Guid id, UpdateItemDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
