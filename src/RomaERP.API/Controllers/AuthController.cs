using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
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

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ITokenService tokenService,
        ITenantContext tenantContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _tenantContext = tenantContext;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var token = _tokenService.GenerateToken(user.Id, user.UserName!, user.Email!, _tenantContext.CompanyCode, roles, modules);

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
}
