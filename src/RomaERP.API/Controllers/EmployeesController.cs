using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUserService _currentUser;
    private readonly IWebHostEnvironment _environment;

    private readonly IPlanLimitGuard? _planLimits;

    public EmployeesController(IEmployeeService employeeService, ICurrentUserService currentUser, IWebHostEnvironment environment, IPlanLimitGuard? planLimits = null)
    {
        _planLimits = planLimits;
        _employeeService = employeeService;
        _currentUser = currentUser;
        _environment = environment;
    }

    [HttpGet]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<EmployeeDto>>> GetAll(CancellationToken ct)
        => Ok(await _employeeService.GetAllAsync(ct));

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeDto>> GetMyProfile(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            return Unauthorized();

        var profile = await _employeeService.GetMyProfileAsync(applicationUserId, ct)
            ?? throw new NotFoundException(nameof(Domain.HR.Employee), applicationUserId);

        return Ok(profile);
    }

    /// <summary>Creates the signed-in user's own employee profile if they don't have one yet (see
    /// IEmployeeService.EnsureMyProfileAsync). Safe to call repeatedly.</summary>
    [HttpPost("me")]
    public async Task<ActionResult<EmployeeDto>> EnsureMyProfile(
        [FromServices] Microsoft.AspNetCore.Identity.UserManager<RomaERP.Infrastructure.Identity.ApplicationUser> userManager, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        return Ok(await _employeeService.EnsureMyProfileAsync(applicationUserId, user?.FullName, user?.Email, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _employeeService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeDto dto, CancellationToken ct)
    {
        if (_planLimits is not null) await _planLimits.EnsureCanAddEmployeeAsync(ct);
        var result = await _employeeService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpdateEmployeeDto dto, CancellationToken ct)
        => Ok(await _employeeService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _employeeService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/face-photo")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<EmployeeDto>> UploadFacePhoto(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");

        if (file.ContentType?.ToLowerInvariant() is not ("image/jpeg" or "image/jpg"))
            throw new ValidationAppException("صورة الوجه المرجعية لازم تكون JPEG.");

        // Content-Type is client-supplied, so also check the real JPEG signature.
        if (!RomaERP.API.Services.UploadSafety.LooksLikeJpeg(await RomaERP.API.Services.UploadSafety.ReadHeadAsync(file, 3, ct)))
            throw new ValidationAppException("صورة الوجه المرجعية لازم تكون JPEG.");

        // The employee must exist in THIS company before a file is written under its id.
        await _employeeService.GetByIdAsync(id, ct);

        var facesDir = Path.Combine(_environment.ContentRootPath, "App_Data", "employee-faces");
        Directory.CreateDirectory(facesDir);

        await using (var stream = System.IO.File.Create(Path.Combine(facesDir, $"{id}.jpg")))
        {
            await file.CopyToAsync(stream, ct);
        }

        return Ok(await _employeeService.SetFaceReferencePhotoAsync(id, $"{id}.jpg", ct));
    }
}
