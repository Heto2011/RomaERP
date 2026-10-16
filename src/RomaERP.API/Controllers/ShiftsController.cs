using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/shifts")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shifts;
    private readonly IEmployeeService _employees;
    private readonly ICurrentUserService _currentUser;

    public ShiftsController(IShiftService shifts, IEmployeeService employees, ICurrentUserService currentUser)
    {
        _shifts = shifts;
        _employees = employees;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> GetShifts(CancellationToken ct) => Ok(await _shifts.GetShiftsAsync(ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<ShiftDto>> Create(ShiftDto dto, CancellationToken ct) => Ok(await _shifts.CreateShiftAsync(dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<ShiftDto>> Update(Guid id, ShiftDto dto, CancellationToken ct) => Ok(await _shifts.UpdateShiftAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _shifts.DeleteShiftAsync(id, ct);
        return NoContent();
    }

    [HttpGet("roster")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<RosterEntryDto>>> GetRoster([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => Ok(await _shifts.GetRosterAsync(from, to, null, ct));

    [HttpPut("roster")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> SetAssignment(SetShiftAssignmentDto dto, CancellationToken ct)
    {
        await _shifts.SetAssignmentAsync(dto, ct);
        return NoContent();
    }

    [HttpPost("roster/copy-week")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<int>> CopyWeek(CopyRosterWeekDto dto, CancellationToken ct) => Ok(await _shifts.CopyWeekAsync(dto, ct));

    /// <summary>The signed-in employee's own schedule.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<RosterEntryDto>>> GetMine([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            throw new ValidationAppException("تعذّر التعرف على المستخدم الحالي.");
        var profile = await _employees.GetMyProfileAsync(applicationUserId, ct)
            ?? throw new ValidationAppException("حسابك مش مربوط بملف موظف — كلّم المسؤول عشان يربطهم.");
        return Ok(await _shifts.GetRosterAsync(from, to, profile.Id, ct));
    }
}
