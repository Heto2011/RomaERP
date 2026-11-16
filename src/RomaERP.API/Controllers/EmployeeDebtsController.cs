using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

/// <summary>What employees owe the company (advances, stock shortages) and its repayment from pay. Nothing here changes pay or the books
/// without the owner or accountant approving it.</summary>
[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/employee-debts")]
public class EmployeeDebtsController : ControllerBase
{
    private readonly IEmployeeDebtService _service;
    public EmployeeDebtsController(IEmployeeDebtService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDebtDto>>> GetAll([FromQuery] Guid? employeeId, CancellationToken ct) => Ok(await _service.GetAllAsync(employeeId, ct));

    [HttpPost("advance")]
    public async Task<ActionResult<EmployeeDebtDto>> CreateAdvance(CreateAdvanceDto dto, CancellationToken ct) => Ok(await _service.CreateAdvanceAsync(dto, User.Identity?.Name, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDebtDto>> Update(Guid id, UpdateDebtDto dto, CancellationToken ct) => Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpGet("payroll/{runId:guid}")]
    public async Task<ActionResult<List<DebtDeductionProposalDto>>> Propose(Guid runId, CancellationToken ct) => Ok(await _service.ProposeForRunAsync(runId, ct));

    [HttpPost("payroll/{runId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid runId, ApproveDeductionsDto dto, CancellationToken ct)
    {
        await _service.ApproveForRunAsync(runId, dto, User.Identity?.Name, ct);
        return NoContent();
    }
}
