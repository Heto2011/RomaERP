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
/// company's users. Admin (the owner/CEO) can do everything; an HR Manager can add people and manage ordinary
/// accounts (HR/Employee) but can never touch an Admin, hand out the Admin/Accountant roles or module grants,
/// or delete accounts — otherwise "can create users" would be a way to become Admin.</summary>
[ApiController]
[Authorize(Roles = "Admin,HR")]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private static readonly string[] ValidRoles = { "Admin", "Accountant", "HR", "Employee" };

    private static readonly string[] HrManageableRoles = { "HR", "Employee" };

    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>True when the caller may change this account: Admin always; an HR Manager only for accounts that
    /// hold nothing beyond the HR/Employee roles (so never an Admin or Accountant).</summary>
    private async Task<bool> CanManageAsync(ApplicationUser target)
    {
        if (IsAdmin) return true;
        var roles = await _userManager.GetRolesAsync(target);
        return roles.All(r => HrManageableRoles.Contains(r));
    }

    /// <summary>Adds a line to the company's activity trail (visible to the platform owner); never affects the request.</summary>
    private Task LogAsync(string category, string action, string? details = null)
        => _activity is null ? Task.CompletedTask : _activity.RecordForCurrentTenantAsync(category, action, details, _currentUser.UserName);

    private ActionResult AdminOnlyResult() => StatusCode(403, new { error = "الإجراء ده للمدير (Admin) بس." });

    private static readonly System.Text.RegularExpressions.Regex PinPattern = new("^[0-9]{4,6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmployeeService _employeeService;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ITenantActivityLog? _activity;

    public UsersController(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser, IEmployeeService employeeService, IPasswordHasher<ApplicationUser> passwordHasher,
        ITenantActivityLog? activity = null)
    {
        _activity = activity;
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
            // An HR Manager never sees the Admin (or any other privileged) accounts — those are the owner's alone.
            if (!IsAdmin && roles.Any(r => !HrManageableRoles.Contains(r))) continue;
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
        if (!await CanManageAsync(user)) return AdminOnlyResult();

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

        if (!IsAdmin && request.Roles.Any(r => !HrManageableRoles.Contains(r)))
            return StatusCode(403, new { error = "تقدر تضيف مستخدمين بدور مدير موارد بشرية أو موظف بس." });

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
        await LogAsync("Users", "User created", $"{user.Email} · {string.Join(", ", request.Roles)}");

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

        if (!IsAdmin && (request.Roles.Any(r => !HrManageableRoles.Contains(r)) || !await CanManageAsync(user)))
            return StatusCode(403, new { error = "تقدر تغيّر أدوار مدير الموارد البشرية والموظفين بس." });

        if (id.ToString() == _currentUser.UserId && !request.Roles.Contains("Admin"))
            return BadRequest(new { error = "متقدرش تشيل دور Admin عن نفسك." });

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRolesAsync(user, request.Roles);
        await LogAsync("Users", "Roles changed", $"{user.Email} → {string.Join(", ", request.Roles)}");

        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles, modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPut("{id:guid}/modules")]
    public async Task<ActionResult<UserDto>> UpdateModules(Guid id, UpdateUserModulesRequest request, CancellationToken ct)
    {
        if (!IsAdmin) return AdminOnlyResult();

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
        if (!await CanManageAsync(user)) return AdminOnlyResult();

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        await LogAsync("Users", "User deactivated", user.Email);

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
        if (!await CanManageAsync(user)) return AdminOnlyResult();

        user.IsActive = true;
        await _userManager.UpdateAsync(user);
        await LogAsync("Users", "User activated", user.Email);

        var roles = await _userManager.GetRolesAsync(user);
        var modules = await GetModulesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), modules, linkedEmployee?.Id, linkedEmployee?.FullNameAr, user.PosPinHash != null));
    }

    [HttpPut("{id:guid}/pos-pin")]
    public async Task<ActionResult<UserDto>> SetPosPin(Guid id, SetPosPinRequest request, CancellationToken ct)
    {
        if (!IsAdmin) return AdminOnlyResult();

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!IsAdmin) return StatusCode(403, new { error = "الإجراء ده للمدير (Admin) بس." });

        if (id.ToString() == _currentUser.UserId)
            return BadRequest(new { error = "متقدرش تمسح حسابك أنت." });

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count(a => a.Id != id) == 0)
                return BadRequest(new { error = "لازم يفضل أدمن واحد على الأقل في الشركة — متقدرش تمسح آخر أدمن." });
        }

        var deletedEmail = user.Email;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("، ", result.Errors.Select(e => e.Description)) });

        await LogAsync("Users", "User deleted", deletedEmail);
        return NoContent();
    }

    /// <summary>Lets an Admin set a new password for ANY user — the only way to recover an account whose
    /// owner forgot their password, since there's no self-service "forgot password" flow. Uses Identity's
    /// reset-token flow instead of touching PasswordHash directly, so the same password-policy validation
    /// (length, complexity) that applies at signup applies here too.</summary>
    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);
        if (!await CanManageAsync(user)) return StatusCode(403, new { error = "الإجراء ده للمدير (Admin) بس." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("، ", result.Errors.Select(e => e.Description)) });

        await LogAsync("Password", "Password changed by an admin", user.Email);
        return NoContent();
    }

    /// <summary>Changes the display name of an account (same who-may-touch-whom rule as the other actions).</summary>
    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, RenameUserRequest request)
    {
        var name = request.FullName?.Trim();
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { error = "الاسم مطلوب." });
        if (name.Length > 200)
            return BadRequest(new { error = "الاسم طويل جدًا." });

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);
        if (!await CanManageAsync(user)) return StatusCode(403, new { error = "الإجراء ده للمدير (Admin) بس." });

        user.FullName = name;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("، ", result.Errors.Select(e => e.Description)) });
        await LogAsync("Users", "User renamed", $"{user.Email} → {name}");

        return NoContent();
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
