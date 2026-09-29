using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IFixedAssetService
{
    Task<List<FixedAssetDto>> GetAllAsync(CancellationToken ct = default);
    Task<FixedAssetDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FixedAssetDto> CreateAsync(CreateFixedAssetDto dto, CancellationToken ct = default);
}
