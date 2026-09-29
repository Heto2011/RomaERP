using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.API.Controllers;

/// <summary>Lets an Admin see this tenant's current usage (active users, active branches) at a glance —
/// purely informational, so they know where they stand before talking pricing with us.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/usage")]
public class UsageController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;

    public UsageController(UserManager<ApplicationUser> userManager, IApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<UsageDto>> GetUsage(CancellationToken ct)
    {
        var activeUsers = await _userManager.Users.CountAsync(u => u.IsActive, ct);
        var activeBranches = await _context.Warehouses.CountAsync(w => w.IsActive, ct);

        return Ok(new UsageDto(activeUsers, activeBranches, DateTime.UtcNow));
    }
}
