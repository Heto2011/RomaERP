using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Audit;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ITenantContext _tenantContext;
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ITokenService tokenService,
        ITenantContext tenantContext,
        IApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _tenantContext = tenantContext;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>IP rate-limited in addition to Identity's per-account lockout — the lockout alone doesn't
    /// stop password spraying across many different tenant accounts from a single source.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            await RecordLoginAsync(null, request.Email, success: false, method: "Password", ct);
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await RecordLoginAsync(user.Id.ToString(), request.Email, success: false, method: "Password", ct);
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var token = _tokenService.GenerateToken(user.Id, user.UserName!, user.Email!, _tenantContext.CompanyCode, roles, modules);

        await RecordLoginAsync(user.Id.ToString(), user.Email!, success: true, method: "Password", ct);

        return Ok(new AuthResponse(token, user.Email!, user.FullName, roles, modules));
    }

    /// <summary>Quick POS entry with a short PIN an Admin set for this user, instead of full email/password —
    /// scans this tenant's active PIN-enabled users for a hash match (small, per-tenant user count, so a
    /// linear scan is fine). IP rate-limited since a short PIN is brute-forceable.</summary>
    [HttpPost("pos-pin-login")]
    [EnableRateLimiting("pos-pin-login")]
    public async Task<ActionResult<AuthResponse>> PosPinLogin(PosPinLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Unauthorized(new { error = "الرقم السري غير صحيح." });

        var candidates = await _userManager.Users
            .Where(u => u.IsActive && u.PosPinHash != null)
            .ToListAsync();

        foreach (var user in candidates)
        {
            if (_passwordHasher.VerifyHashedPassword(user, user.PosPinHash!, request.Pin) == PasswordVerificationResult.Success)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var modules = await GetModulesAsync(user);
                var token = _tokenService.GenerateToken(user.Id, user.UserName!, user.Email!, _tenantContext.CompanyCode, roles, modules);
                await RecordLoginAsync(user.Id.ToString(), user.Email!, success: true, method: "PosPin", CancellationToken.None);
                return Ok(new AuthResponse(token, user.Email!, user.FullName, roles, modules));
            }
        }

        return Unauthorized(new { error = "الرقم السري غير صحيح." });
    }

    private async Task<List<string>> GetModulesAsync(ApplicationUser user)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        return claims.Where(c => c.Type == ModulePermissions.ClaimType).Select(c => c.Value).ToList();
    }

    /// <summary>Records who tried to log in, when, and from which IP — nginx sits in front of this API and
    /// sets X-Real-IP, so that takes precedence over the (otherwise-proxy) connection address.</summary>
    private async Task RecordLoginAsync(string? userId, string userName, bool success, string method, CancellationToken ct)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ip = httpContext?.Request.Headers["X-Real-IP"].FirstOrDefault()
            ?? httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? httpContext?.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        _context.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            UserName = userName,
            IpAddress = ip,
            Success = success,
            Method = method,
        });
        await _context.SaveChangesAsync(ct);
    }
}
