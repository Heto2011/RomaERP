namespace RomaERP.API.Contracts;

public record UserDto(Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles, Guid? EmployeeId, string? EmployeeName);

public record CreateUserRequest(string Email, string Password, string FullName, List<string> Roles);

public record UpdateUserRolesRequest(List<string> Roles);

public record LinkEmployeeRequest(Guid? EmployeeId);
