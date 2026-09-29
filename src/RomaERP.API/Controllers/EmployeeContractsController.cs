using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.HRPolicy)]
[Route("api/employee-contracts")]
public class EmployeeContractsController : ControllerBase
{
    private readonly IEmployeeContractService _contractService;

    public EmployeeContractsController(IEmployeeContractService contractService)
    {
        _contractService = contractService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeContractDto>>> GetAll(CancellationToken ct)
        => Ok(await _contractService.GetAllAsync(ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<List<EmployeeContractDto>>> GetForEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _contractService.GetForEmployeeAsync(employeeId, ct));

    [HttpPost]
    public async Task<ActionResult<EmployeeContractDto>> Create(CreateEmployeeContractDto dto, CancellationToken ct)
        => Ok(await _contractService.CreateAsync(dto, ct));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<EmployeeContractDto>> UpdateStatus(Guid id, UpdateEmployeeContractStatusDto dto, CancellationToken ct)
        => Ok(await _contractService.UpdateStatusAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _contractService.DeleteAsync(id, ct);
        return NoContent();
    }
}
