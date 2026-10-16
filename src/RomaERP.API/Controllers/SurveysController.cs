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
[Route("api/surveys")]
public class SurveysController : ControllerBase
{
    private readonly ISurveyService _service;
    private readonly IEmployeeService _employees;
    private readonly ICurrentUserService _currentUser;

    public SurveysController(ISurveyService service, IEmployeeService employees, ICurrentUserService currentUser)
    {
        _service = service;
        _employees = employees;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<List<SurveyDto>>> GetAll(CancellationToken ct) => Ok(await _service.GetSurveysAsync(ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<SurveyDto>> Create(SurveyDto dto, CancellationToken ct) => Ok(await _service.SaveSurveyAsync(null, dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<SurveyDto>> Update(Guid id, SurveyDto dto, CancellationToken ct) => Ok(await _service.SaveSurveyAsync(id, dto, ct));

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<SurveyDto>> SetStatus(Guid id, [FromQuery] SurveyStatus status, CancellationToken ct) => Ok(await _service.SetStatusAsync(id, status, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteSurveyAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/results")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<SurveyResultsDto>> Results(Guid id, CancellationToken ct) => Ok(await _service.GetResultsAsync(id, ct));

    /// <summary>Open surveys for the signed-in employee, flagged if already answered.</summary>
    [HttpGet("open")]
    public async Task<ActionResult<List<SurveyDto>>> GetOpen(CancellationToken ct) => Ok(await _service.GetOpenForEmployeeAsync(await MyEmployeeIdAsync(ct), ct));

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, SubmitSurveyDto dto, CancellationToken ct)
    {
        await _service.SubmitAsync(await MyEmployeeIdAsync(ct), id, dto, ct);
        return NoContent();
    }

    private async Task<Guid> MyEmployeeIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            throw new ValidationAppException("تعذّر التعرف على المستخدم الحالي.");
        var profile = await _employees.GetMyProfileAsync(applicationUserId, ct)
            ?? throw new ValidationAppException("حسابك مش مربوط بملف موظف — كلّم المسؤول عشان يربطهم.");
        return profile.Id;
    }
}
