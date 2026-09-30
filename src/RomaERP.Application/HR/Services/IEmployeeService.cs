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

    /// <summary>Returns the employee profile linked to this login, creating a minimal one (first department and
    /// position, zero salary, hired today) if there is none. Lets a company owner or HR admin — who signs up as a
    /// user, not as an employee — record their own attendance and requests without an admin linking them first.</summary>
    Task<EmployeeDto> EnsureMyProfileAsync(Guid applicationUserId, string? fullName, string? email, CancellationToken ct = default);
    Task<EmployeeDto> LinkUserAsync(Guid employeeId, Guid? applicationUserId, CancellationToken ct = default);
    Task<EmployeeDto> SetFaceReferencePhotoAsync(Guid employeeId, string storedFileName, CancellationToken ct = default);
}
