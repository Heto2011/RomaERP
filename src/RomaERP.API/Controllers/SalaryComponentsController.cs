using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.API.Contracts;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.HRPolicy)]
[Route("api/[controller]")]
public class SalaryComponentsController : ControllerBase
{
    private readonly ISalaryComponentService _salaryComponentService;

    public SalaryComponentsController(ISalaryComponentService salaryComponentService)
    {
        _salaryComponentService = salaryComponentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SalaryComponentDto>>> GetAll(CancellationToken ct)
        => Ok(await _salaryComponentService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<ActionResult<SalaryComponentDto>> Create(CreateSalaryComponentDto dto, CancellationToken ct)
        => Ok(await _salaryComponentService.CreateAsync(dto, ct));

    [HttpGet("employees/{employeeId:guid}")]
    public async Task<ActionResult<List<EmployeeSalaryComponentDto>>> GetForEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _salaryComponentService.GetForEmployeeAsync(employeeId, ct));

    [HttpPost("employees/{employeeId:guid}/assign")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> AssignToEmployee(Guid employeeId, AssignSalaryComponentRequest request, CancellationToken ct)
    {
        await _salaryComponentService.AssignToEmployeeAsync(employeeId, request.SalaryComponentId, request.Value, ct);
        return NoContent();
    }

    [HttpDelete("employees/{employeeId:guid}/{salaryComponentId:guid}")]
    [Authorize(Policy = ModulePermissions.HRPolicy)]
    public async Task<IActionResult> RemoveFromEmployee(Guid employeeId, Guid salaryComponentId, CancellationToken ct)
    {
        await _salaryComponentService.RemoveFromEmployeeAsync(employeeId, salaryComponentId, ct);
        return NoContent();
    }
}
