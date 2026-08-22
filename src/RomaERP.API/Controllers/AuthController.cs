using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ITenantContext _tenantContext;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ITokenService tokenService, ITenantContext tenantContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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
        var token = _tokenService.GenerateToken(user.Id, user.UserName!, user.Email!, _tenantContext.CompanyCode, roles);

        return Ok(new AuthResponse(token, user.Email!, user.FullName, roles));
    }
}
