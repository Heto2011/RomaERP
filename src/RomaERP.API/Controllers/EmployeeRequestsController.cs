using System.Security.Claims;
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
[Route("api/employee-requests")]
public class EmployeeRequestsController : ControllerBase
{
    private readonly IEmployeeRequestService _requestService;
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUserService _currentUser;

    public EmployeeRequestsController(IEmployeeRequestService requestService, IEmployeeService employeeService, ICurrentUserService currentUser)
    {
        _requestService = requestService;
        _employeeService = employeeService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeRequestDto>> Create(CreateEmployeeRequestDto dto, CancellationToken ct)
    {
        var employeeId = await ResolveMyEmployeeIdAsync(ct);
        return Ok(await _requestService.CreateAsync(employeeId, dto, ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<EmployeeRequestDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = await ResolveMyEmployeeIdAsync(ct);
        return Ok(await _requestService.GetMineAsync(employeeId, ct));
    }

    [HttpGet("my-leave-balance")]
    public async Task<ActionResult<LeaveBalanceDto>> GetMyLeaveBalance(CancellationToken ct)
    {
        var employeeId = await ResolveMyEmployeeIdAsync(ct);
        return Ok(await _requestService.GetLeaveBalanceAsync(employeeId, ct));
    }

    [HttpGet("pending")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<EmployeeRequestDto>>> GetPending(CancellationToken ct)
        => Ok(await _requestService.GetPendingAsync(ct));

    [HttpGet]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<EmployeeRequestDto>>> GetAll(CancellationToken ct)
        => Ok(await _requestService.GetAllAsync(ct));

    [HttpPost("{id:guid}/decide")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<EmployeeRequestDto>> Decide(Guid id, DecideEmployeeRequestDto dto, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(userId, out var decidedByUserId))
            return Unauthorized();

        return Ok(await _requestService.DecideAsync(id, decidedByUserId, dto, ct));
    }

    private async Task<Guid> ResolveMyEmployeeIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            throw new ValidationAppException("تعذّر التعرف على المستخدم الحالي.");

        var profile = await _employeeService.GetMyProfileAsync(applicationUserId, ct)
            ?? throw new ValidationAppException("حسابك مش مربوط بملف موظف — كلّم المسؤول عشان يربطهم.");

        return profile.Id;
    }
}
