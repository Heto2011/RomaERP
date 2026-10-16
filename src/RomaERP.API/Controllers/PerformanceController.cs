using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/performance")]
public class PerformanceController : ControllerBase
{
    private readonly IPerformanceService _service;
    private readonly IEmployeeService _employees;
    private readonly ICurrentUserService _currentUser;

    public PerformanceController(IPerformanceService service, IEmployeeService employees, ICurrentUserService currentUser)
    {
        _service = service;
        _employees = employees;
        _currentUser = currentUser;
    }

    [HttpGet("cycles")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<ReviewCycleDto>>> GetCycles(CancellationToken ct) => Ok(await _service.GetCyclesAsync(ct));

    [HttpPost("cycles")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<ReviewCycleDto>> CreateCycle(ReviewCycleDto dto, CancellationToken ct) => Ok(await _service.SaveCycleAsync(null, dto, ct));

    [HttpPut("cycles/{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<ReviewCycleDto>> UpdateCycle(Guid id, ReviewCycleDto dto, CancellationToken ct) => Ok(await _service.SaveCycleAsync(id, dto, ct));

    [HttpPost("cycles/{id:guid}/status")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<ReviewCycleDto>> SetStatus(Guid id, [FromQuery] ReviewCycleStatus status, CancellationToken ct) => Ok(await _service.SetStatusAsync(id, status, ct));

    [HttpGet("cycles/{id:guid}/assignments")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<ReviewAssignmentDto>>> GetAssignments(Guid id, CancellationToken ct) => Ok(await _service.GetAssignmentsAsync(id, ct));

    [HttpPost("cycles/{id:guid}/assignments")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<int>> AddAssignments(Guid id, AddReviewAssignmentsDto dto, CancellationToken ct) => Ok(await _service.AddAssignmentsAsync(id, dto, ct));

    [HttpPost("cycles/{id:guid}/self-reviews")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<int>> AddSelfReviews(Guid id, CancellationToken ct) => Ok(await _service.AddSelfReviewsAsync(id, ct));

    [HttpDelete("assignments/{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken ct)
    {
        await _service.DeleteAssignmentAsync(id, ct);
        return NoContent();
    }

    [HttpGet("cycles/{id:guid}/results")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<ReviewSubjectResultDto>>> GetResults(Guid id, CancellationToken ct) => Ok(await _service.GetResultsAsync(id, ct));

    /// <summary>The reviews the signed-in employee has been asked to fill in.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<ReviewAssignmentDto>>> GetMine(CancellationToken ct)
        => Ok(await _service.GetMineAsync(await MyEmployeeIdAsync(ct), ct));

    [HttpPost("assignments/{id:guid}/submit")]
    public async Task<ActionResult<ReviewAssignmentDto>> Submit(Guid id, SubmitReviewDto dto, CancellationToken ct)
        => Ok(await _service.SubmitAsync(await MyEmployeeIdAsync(ct), id, dto, ct));

    private async Task<Guid> MyEmployeeIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            throw new ValidationAppException("تعذّر التعرف على المستخدم الحالي.");
        var profile = await _employees.GetMyProfileAsync(applicationUserId, ct)
            ?? throw new ValidationAppException("حسابك مش مربوط بملف موظف — كلّم المسؤول عشان يربطهم.");
        return profile.Id;
    }
}
