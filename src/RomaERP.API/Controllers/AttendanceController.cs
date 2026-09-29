using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUserService _currentUser;
    private readonly IWebHostEnvironment _environment;

    public AttendanceController(IAttendanceService attendanceService, IEmployeeService employeeService, ICurrentUserService currentUser, IWebHostEnvironment environment)
    {
        _attendanceService = attendanceService;
        _employeeService = employeeService;
        _currentUser = currentUser;
        _environment = environment;
    }

    [HttpPost("check-in")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AttendanceRecordDto>> CheckIn([FromForm] decimal latitude, [FromForm] decimal longitude, IFormFile? selfie, CancellationToken ct)
    {
        var employeeId = await ResolveMyEmployeeIdAsync(ct);
        var (referenceBytes, selfieBytes) = await LoadFaceBytesAsync(employeeId, selfie, ct);
        return Ok(await _attendanceService.CheckInAsync(employeeId, latitude, longitude, referenceBytes, selfieBytes, ct));
    }

    [HttpPost("check-out")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AttendanceRecordDto>> CheckOut([FromForm] decimal latitude, [FromForm] decimal longitude, IFormFile? selfie, CancellationToken ct)
    {
        var employeeId = await ResolveMyEmployeeIdAsync(ct);
        var (referenceBytes, selfieBytes) = await LoadFaceBytesAsync(employeeId, selfie, ct);
        return Ok(await _attendanceService.CheckOutAsync(employeeId, latitude, longitude, referenceBytes, selfieBytes, ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetMine([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var employeeId = await ResolveMyEmployeeIdAsync(ct);
        return Ok(await _attendanceService.GetMineAsync(employeeId, from, to, ct));
    }

    [HttpGet]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? employeeId, CancellationToken ct)
        => Ok(await _attendanceService.GetAllAsync(from, to, employeeId, ct));

    private async Task<Guid> ResolveMyEmployeeIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            throw new ValidationAppException("تعذّر التعرف على المستخدم الحالي.");

        var profile = await _employeeService.GetMyProfileAsync(applicationUserId, ct)
            ?? throw new ValidationAppException("حسابك مش مربوط بملف موظف — كلّم المسؤول عشان يربطهم.");

        return profile.Id;
    }

    private async Task<(byte[]? referenceBytes, byte[]? selfieBytes)> LoadFaceBytesAsync(Guid employeeId, IFormFile? selfie, CancellationToken ct)
    {
        var facesDir = Path.Combine(_environment.ContentRootPath, "App_Data", "employee-faces");
        var referencePath = Path.Combine(facesDir, $"{employeeId}.jpg");
        byte[]? referenceBytes = System.IO.File.Exists(referencePath)
            ? await System.IO.File.ReadAllBytesAsync(referencePath, ct)
            : null;

        byte[]? selfieBytes = null;
        if (selfie is { Length: > 0 })
        {
            using var memoryStream = new MemoryStream();
            await selfie.CopyToAsync(memoryStream, ct);
            selfieBytes = memoryStream.ToArray();
        }

        return (referenceBytes, selfieBytes);
    }
}
