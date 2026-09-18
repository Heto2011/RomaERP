using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IEmployeeContractService
{
    Task<List<EmployeeContractDto>> GetAllAsync(CancellationToken ct = default);
    Task<List<EmployeeContractDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<EmployeeContractDto> CreateAsync(CreateEmployeeContractDto dto, CancellationToken ct = default);
    Task<EmployeeContractDto> UpdateStatusAsync(Guid id, UpdateEmployeeContractStatusDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Active contracts whose EndDate is within <paramref name="days"/> from now (already-overdue
    /// ones included) — used by the Alerts system for renewal reminders.</summary>
    Task<List<EmployeeContractDto>> GetExpiringAsync(int days, CancellationToken ct = default);
}
