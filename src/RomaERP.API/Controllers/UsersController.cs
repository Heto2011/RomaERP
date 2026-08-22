using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
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

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmployeeService _employeeService;

    public UsersController(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser, IEmployeeService employeeService)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _employeeService = employeeService;
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
            var linkedEmployee = employeeByUserId.GetValueOrDefault(user.Id);
            result.Add(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), linkedEmployee?.Id, linkedEmployee?.FullNameAr));
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
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), linkedEmployee?.Id, linkedEmployee?.FullNameAr));
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

        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles, null, null));
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

        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, request.Roles, linkedEmployee?.Id, linkedEmployee?.FullNameAr));
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
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), linkedEmployee?.Id, linkedEmployee?.FullNameAr));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<UserDto>> Activate(Guid id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(ApplicationUser), id);

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var linkedEmployee = await GetLinkedEmployeeAsync(id, ct);
        return Ok(new UserDto(user.Id, user.Email!, user.FullName, user.IsActive, roles.ToList(), linkedEmployee?.Id, linkedEmployee?.FullNameAr));
    }

    private async Task<Application.HR.DTOs.EmployeeDto?> GetLinkedEmployeeAsync(Guid userId, CancellationToken ct)
    {
        var employees = await _employeeService.GetAllAsync(ct);
        return employees.FirstOrDefault(e => e.ApplicationUserId == userId);
    }
}
