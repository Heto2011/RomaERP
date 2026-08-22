using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Interfaces;
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

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;

    public UsersController(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList()));
        }

        return Ok(result);
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

        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles));
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult<UserDto>> UpdateRoles(Guid id, UpdateUserRolesRequest request)
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

        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<UserDto>> Deactivate(Guid id)
    {
        if (id.ToString() == _currentUser.UserId)
            return BadRequest(new { error = "متقدرش توقف حسابك أنت." });

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList()));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<UserDto>> Activate(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList()));
    }
}
