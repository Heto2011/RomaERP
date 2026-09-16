using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.Services;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

/// <summary>Any authenticated tenant user could otherwise view or close a DIFFERENT cashier's open shift by
/// guessing/enumerating their employeeId/shiftId — a real cash-reconciliation-tampering risk in a POS. Every
/// action here is restricted to the caller's own linked employee record unless they're an Admin.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CashierShiftsController : ControllerBase
{
    private readonly ICashierShiftService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmployeeService _employeeService;

    public CashierShiftsController(ICashierShiftService service, ICurrentUserService currentUser, IEmployeeService employeeService)
    {
        _service = service;
        _currentUser = currentUser;
        _employeeService = employeeService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<CashierShiftDto?>> GetActive([FromQuery] Guid employeeId, CancellationToken ct)
    {
        if (!await CanActAsAsync(employeeId, ct)) return Forbid();
        return Ok(await _service.GetActiveShiftAsync(employeeId, ct));
    }

    [HttpPost("open")]
    public async Task<ActionResult<CashierShiftDto>> Open(OpenCashierShiftDto dto, CancellationToken ct)
    {
        if (!await CanActAsAsync(dto.EmployeeId, ct)) return Forbid();
        return Ok(await _service.OpenAsync(dto, ct));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<CashierShiftDto>> Close(Guid id, CloseCashierShiftDto dto, CancellationToken ct)
    {
        var active = await _service.GetActiveShiftAsync(await ResolveMyEmployeeIdOrNullAsync(ct) ?? Guid.Empty, ct);
        if (!User.IsInRole("Admin") && (active is null || active.Id != id)) return Forbid();
        return Ok(await _service.CloseAsync(id, dto, ct));
    }

    /// <summary>An Admin can act on behalf of any employee (e.g. force-closing a shift left open); everyone
    /// else may only act as the employee record linked to their own account.</summary>
    private async Task<bool> CanActAsAsync(Guid employeeId, CancellationToken ct)
    {
        if (User.IsInRole("Admin")) return true;
        var myEmployeeId = await ResolveMyEmployeeIdOrNullAsync(ct);
        return myEmployeeId is not null && myEmployeeId == employeeId;
    }

    private async Task<Guid?> ResolveMyEmployeeIdOrNullAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            return null;

        var profile = await _employeeService.GetMyProfileAsync(applicationUserId, ct);
        return profile?.Id;
    }
}
