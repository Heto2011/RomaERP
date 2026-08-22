namespace RomaERP.API.Contracts;

public record UserDto(Guid Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles);

public record CreateUserRequest(string Email, string Password, string FullName, List<string> Roles);

public record UpdateUserRolesRequest(List<string> Roles);
