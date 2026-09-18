using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

/// <summary>Read-only view of login attempts (success and failure) — who used the system, when, and from
/// which IP address.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/login-history")]
public class LoginHistoryController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public LoginHistoryController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<LoginHistoryDto>>> Get(
        [FromQuery] string? userId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take,
        CancellationToken ct)
    {
        var query = _context.LoginHistories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(l => l.UserId == userId);
        if (fromUtc.HasValue) query = query.Where(l => l.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(l => l.OccurredAtUtc <= toUtc.Value);

        var pageSize = take <= 0 ? 200 : Math.Clamp(take, 1, 500);

        var logs = await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(logs.Select(l => new LoginHistoryDto(l.Id, l.UserId, l.UserName, l.IpAddress, l.Success, l.Method, l.OccurredAtUtc)).ToList());
    }
}
