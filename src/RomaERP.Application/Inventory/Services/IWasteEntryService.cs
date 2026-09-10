using RomaERP.Application.Inventory.DTOs;

namespace RomaERP.Application.Inventory.Services;

public interface IWasteEntryService
{
    Task<List<WasteEntryDto>> GetAllAsync(CancellationToken ct = default);
    Task<WasteEntryDto> CreateAsync(CreateWasteEntryDto dto, CancellationToken ct = default);
}
