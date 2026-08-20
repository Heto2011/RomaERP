using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;

    public PayrollController(IPayrollService payrollService)
    {
        _payrollService = payrollService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PayrollRunDto>>> GetAll(CancellationToken ct)
        => Ok(await _payrollService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PayrollRunDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _payrollService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PayrollRunDto>> Create(CreatePayrollRunDto dto, CancellationToken ct)
    {
        var result = await _payrollService.CreateAndCalculateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PayrollRunDto>> Approve(Guid id, CancellationToken ct)
        => Ok(await _payrollService.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/post")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<PayrollRunDto>> Post(Guid id, CancellationToken ct)
        => Ok(await _payrollService.PostAsync(id, ct));
}
