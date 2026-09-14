using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.Services;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.API.Controllers;

/// <summary>Manages the users of the current tenant only — UserManager/RoleManager here are bound to this
/// request's tenant database (see DependencyInjection.AddInfrastructure), so this can never touch another
/// company's users. Admin-only, since this is account/access management.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private static readonly string[] ValidRoles = { "Admin", "Accountant", "HR", "Employee" };

    private static readonly System.Text.RegularExpressions.Regex PinPattern = new("^[0-9]{4,6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmployeeService _employeeService;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UsersController(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser, IEmployeeService employeeService, IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _employeeService = employeeService;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var employees = await _employeeService.GetAllAsync(ct);
        var employeeByUserId = employees.Where(e => e.ApplicationUserId is not null).ToDictionary(e => e.ApplicationUserId!.Value);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var modules = await GetModulesAsync(user);
            var linkedEmployee = employeeByUserId.GetValueOrDefault(user.Id);
            result.Add(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}/employee-link")]
    public async Task<ActionResult<UserDto>> LinkEmployee(Guid id, LinkEmployeeRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        if (request.EmployeeId is { } employeeId)
            await _employeeService.LinkUserAsync(employeeId, id, ct);
        else if (await GetLinkedEmployeeAsync(id, ct) is { } currentlyLinked)
            await _employeeService.LinkUserAsync(currentlyLinked.Id, null, ct);

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { error = "البريد الإلكتروني والاسم مطلوبين." });

        if (request.Roles.Count == 0)
            return BadRequest(new { error = "لازم تحدد دور واحد على الأقل للمستخدم." });

        var unknownRole = request.Roles.FirstOrDefault(r => !ValidRoles.Contains(r));
        if (unknownRole is not null)
            return BadRequest(new { error = $"دور غير معروف: {unknownRole}" });

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            return BadRequest(new { error = "البريد الإلكتروني ده مستخدم قبل كده." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { error = string.Join("، ", createResult.Errors.Select(e => e.Description)) });

        await _userManager.AddToRolesAsync(user, request.Roles);

        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles, Array.Empty<string>(), null, null, false));
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult<UserDto>> UpdateRoles(Guid id, UpdateUserRolesRequest request, CancellationToken ct)
    {
        if (request.Roles.Count == 0)
            return BadRequest(new { error = "لازم يفضل دور واحد على الأقل للمستخدم." });

        var unknownRole = request.Roles.FirstOrDefault(r => !ValidRoles.Contains(r));
        if (unknownRole is not null)
            return BadRequest(new { error = $"دور غير معروف: {unknownRole}" });

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        if (id.ToString() == _currentUser.UserId && !request.Roles.Contains("Admin"))
            return BadRequest(new { error = "متقدرش تشيل دور Admin عن نفسك." });

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRolesAsync(user, request.Roles);

        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles, modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPut("{id:guid}/modules")]
    public async Task<ActionResult<UserDto>> UpdateModules(Guid id, UpdateUserModulesRequest request, CancellationToken ct)
    {
        var unknownModule = request.Modules.FirstOrDefault(m => !ModulePermissions.All.Contains(m));
        if (unknownModule is not null)
            return BadRequest(new { error = $"وحدة صلاحيات غير معروفة: {unknownModule}" });

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        var existingClaims = (await _userManager.GetClaimsAsync(user))
            .Where(c => c.Type == ModulePermissions.ClaimType)
            .ToList();
        if (existingClaims.Count > 0)
            await _userManager.RemoveClaimsAsync(user, existingClaims);

        var newModules = request.Modules.Distinct().ToList();
        if (newModules.Count > 0)
            await _userManager.AddClaimsAsync(user, newModules.Select(m => new Claim(ModulePermissions.ClaimType, m)));

        var roles = await _userManager.GetRolesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), newModules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<UserDto>> Deactivate(Guid id, CancellationToken ct)
    {
        if (id.ToString() == _currentUser.UserId)
            return BadRequest(new { error = "متقدرش توقف حسابك أنت." });

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<UserDto>> Activate(Guid id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPut("{id:guid}/pos-pin")]
    public async Task<ActionResult<UserDto>> SetPosPin(Guid id, SetPosPinRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            user.PosPinHash = null;
        }
        else
        {
            if (!PinPattern.IsMatch(request.Pin))
                return BadRequest(new { error = "الرقم السري لازم يكون أرقام بس، من 4 لـ 6 أرقام." });

            var otherUsers = await _userManager.Users
                .Where(u => u.Id != id && u.IsActive && u.PosPinHash != null)
                .ToListAsync(ct);
            if (otherUsers.Any(u => _passwordHasher.VerifyHashedPassword(u, u.PosPinHash!, request.Pin) == PasswordVerificationResult.Success))
                return BadRequest(new { error = "الرقم السري ده مستخدم بالفعل من مستخدم تاني، اختار رقم مختلف." });

            user.PosPinHash = _passwordHasher.HashPassword(user, request.Pin);
        }

        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    private async Task<Application.HR.DTOs.EmployeeDto?> GetLinkedEmployeeAsync(Guid userId, CancellationToken ct)
    {
        var employees = await _employeeService.GetAllAsync(ct);
        return employees.FirstOrDefault(e => e.ApplicationUserId == userId);
    }

    private async Task<List<string>> GetModulesAsync(ApplicationUser user)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        return claims.Where(c => c.Type == ModulePermissions.ClaimType).Select(c => c.Value).ToList();
    }
}
