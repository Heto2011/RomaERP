using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

public interface IPhysicalStockCountService
{
    Task<List<PhysicalStockCountDto>> GetAllAsync(CancellationToken ct = default);
    Task<PhysicalStockCountDto> CreateAsync(CreatePhysicalStockCountDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
