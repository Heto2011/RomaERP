using RomaERP.Application.Accounting.DTOs;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public interface IManualProfitEntryService
{
    Task<List<ManualProfitEntryDto>> GetAllAsync(ManualProfitDimension dimension, CancellationToken ct = default);
    Task<ManualProfitEntryDto> CreateAsync(CreateManualProfitEntryDto dto, CancellationToken ct = default);
    Task<ManualProfitEntryDto> UpdateAsync(Guid id, UpdateManualProfitEntryDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
