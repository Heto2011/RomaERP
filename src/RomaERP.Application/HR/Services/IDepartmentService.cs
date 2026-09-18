using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync(CancellationToken ct = default);
    Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default);
    Task<DepartmentDto> UpdateAsync(Guid id, CreateDepartmentDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IPositionService
{
    Task<List<PositionDto>> GetAllAsync(CancellationToken ct = default);
    Task<PositionDto> CreateAsync(CreatePositionDto dto, CancellationToken ct = default);
    Task<PositionDto> UpdateAsync(Guid id, CreatePositionDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
