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

    public EmployeesController(IEmployeeService employeeService, ICurrentUserService currentUser)
    {
        _employeeService = employeeService;
        _currentUser = currentUser;
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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _employeeService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeDto dto, CancellationToken ct)
    {
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
}
