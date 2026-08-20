namespace RomaERP.API.Contracts;

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email, string FullName, IEnumerable<string> Roles);
