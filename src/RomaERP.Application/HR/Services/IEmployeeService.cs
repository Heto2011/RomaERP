using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetAllAsync(CancellationToken ct = default);
    Task<EmployeeDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDto?> GetMyProfileAsync(Guid applicationUserId, CancellationToken ct = default);
    Task<EmployeeDto> LinkUserAsync(Guid employeeId, Guid? applicationUserId, CancellationToken ct = default);
}
