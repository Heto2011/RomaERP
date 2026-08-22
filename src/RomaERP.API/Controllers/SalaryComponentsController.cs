using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.API.Contracts;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,HR")]
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
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<SalaryComponentDto>> Create(CreateSalaryComponentDto dto, CancellationToken ct)
        => Ok(await _salaryComponentService.CreateAsync(dto, ct));

    [HttpPost("employees/{employeeId:guid}/assign")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> AssignToEmployee(Guid employeeId, AssignSalaryComponentRequest request, CancellationToken ct)
    {
        await _salaryComponentService.AssignToEmployeeAsync(employeeId, request.SalaryComponentId, request.Value, ct);
        return NoContent();
    }

    [HttpDelete("employees/{employeeId:guid}/{salaryComponentId:guid}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> RemoveFromEmployee(Guid employeeId, Guid salaryComponentId, CancellationToken ct)
    {
        await _salaryComponentService.RemoveFromEmployeeAsync(employeeId, salaryComponentId, ct);
        return NoContent();
    }
}
