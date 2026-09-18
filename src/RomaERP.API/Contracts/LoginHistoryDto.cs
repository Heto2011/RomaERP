namespace RomaERP.API.Contracts;

public record LoginHistoryDto(
    Guid Id,
    string? UserId,
    string UserName,
    string IpAddress,
    bool Success,
    string Method,
    DateTime OccurredAtUtc);
