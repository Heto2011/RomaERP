using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IWorkLocationService
{
    Task<List<WorkLocationDto>> GetAllAsync(CancellationToken ct = default);
    Task<WorkLocationDto> CreateAsync(SaveWorkLocationDto dto, CancellationToken ct = default);
    Task<WorkLocationDto> UpdateAsync(Guid id, SaveWorkLocationDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
