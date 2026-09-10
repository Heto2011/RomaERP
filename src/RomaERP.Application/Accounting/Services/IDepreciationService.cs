using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IDepreciationService
{
    Task<List<DepreciationRunDto>> GetAllAsync(CancellationToken ct = default);
    Task<DepreciationRunDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DepreciationRunDto> CreateAndCalculateAsync(CreateDepreciationRunDto dto, CancellationToken ct = default);
    Task<DepreciationRunDto> PostAsync(Guid id, CancellationToken ct = default);
}
