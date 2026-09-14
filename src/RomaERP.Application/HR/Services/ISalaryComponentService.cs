using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface ISalaryComponentService
{
    Task<List<SalaryComponentDto>> GetAllAsync(CancellationToken ct = default);
    Task<SalaryComponentDto> CreateAsync(CreateSalaryComponentDto dto, CancellationToken ct = default);
    Task<List<EmployeeSalaryComponentDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task AssignToEmployeeAsync(Guid employeeId, Guid salaryComponentId, decimal value, CancellationToken ct = default);
    Task RemoveFromEmployeeAsync(Guid employeeId, Guid salaryComponentId, CancellationToken ct = default);
}
