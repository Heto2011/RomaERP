namespace RomaERP.API.Contracts;

public record LoginRequest(string Email, string Password);

public record PosPinLoginRequest(string Pin);

public record AuthResponse(string Token, string Email, string FullName, IEnumerable<string> Roles, IEnumerable<string> Modules);
