using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUserService _currentUser;

    public PayrollController(IPayrollService payrollService, IEmployeeService employeeService, ICurrentUserService currentUser)
    {
        _payrollService = payrollService;
        _employeeService = employeeService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,HR,Accountant")]
    public async Task<ActionResult<List<PayrollRunDto>>> GetAll(CancellationToken ct)
        => Ok(await _payrollService.GetAllAsync(ct));

    [HttpGet("me")]
    public async Task<ActionResult<List<MyPayslipDto>>> GetMyPayslips(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            return Unauthorized();

        var profile = await _employeeService.GetMyProfileAsync(applicationUserId, ct);
        if (profile is null)
            return Ok(new List<MyPayslipDto>());

        return Ok(await _payrollService.GetMyPayslipsAsync(profile.Id, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,HR,Accountant")]
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
