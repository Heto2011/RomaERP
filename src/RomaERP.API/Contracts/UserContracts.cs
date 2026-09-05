namespace RomaERP.API.Contracts;

public record UserDto(Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles, IReadOnlyList<string> Modules, Guid? EmployeeId, string? EmployeeName, bool HasPosPin);

public record CreateUserRequest(string Email, string Password, string FullName, List<string> Roles);

public record UpdateUserRolesRequest(List<string> Roles);

public record UpdateUserModulesRequest(List<string> Modules);

public record LinkEmployeeRequest(Guid? EmployeeId);

/// <summary>Set a null/empty Pin to clear it (disabling PIN login for this user).</summary>
public record SetPosPinRequest(string? Pin);
