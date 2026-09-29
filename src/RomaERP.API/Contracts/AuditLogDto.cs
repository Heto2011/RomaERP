namespace RomaERP.API.Contracts;

public record AuditLogDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string? UserId,
    string? UserName,
    DateTime OccurredAtUtc,
    string Changes);
