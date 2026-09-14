using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

/// <summary>Read-only view over the automatic change log EF Core records on every save — who created,
/// updated, or (soft-)deleted which record, and when.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/audit-log")]
public class AuditLogController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AuditLogController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> Get(
        [FromQuery] string? entityName,
        [FromQuery] string? userId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take,
        CancellationToken ct)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(a => a.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(a => a.UserId == userId);
        if (fromUtc.HasValue) query = query.Where(a => a.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(a => a.OccurredAtUtc <= toUtc.Value);

        var pageSize = take <= 0 ? 200 : Math.Clamp(take, 1, 500);

        var logs = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(logs.Select(a => new AuditLogDto(a.Id, a.EntityName, a.EntityId, a.Action.ToString(), a.UserId, a.UserName, a.OccurredAtUtc, a.Changes)).ToList());
    }

    [HttpGet("entity-names")]
    public async Task<ActionResult<List<string>>> GetEntityNames(CancellationToken ct)
    {
        var names = await _context.AuditLogs.AsNoTracking()
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
        return Ok(names);
    }
}
