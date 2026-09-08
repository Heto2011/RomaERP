using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IEmployeeRequestService
{
    Task<EmployeeRequestDto> CreateAsync(Guid employeeId, CreateEmployeeRequestDto dto, CancellationToken ct = default);
    Task<List<EmployeeRequestDto>> GetMineAsync(Guid employeeId, CancellationToken ct = default);
    Task<List<EmployeeRequestDto>> GetPendingAsync(CancellationToken ct = default);
    Task<List<EmployeeRequestDto>> GetAllAsync(CancellationToken ct = default);
    Task<EmployeeRequestDto> DecideAsync(Guid id, Guid decidedByUserId, DecideEmployeeRequestDto dto, CancellationToken ct = default);
}
