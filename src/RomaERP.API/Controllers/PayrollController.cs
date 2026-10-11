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

    /// <summary>One employee's payslip as a PDF, for HR and accounting.</summary>
    [HttpGet("{id:guid}/payslip/{employeeId:guid}")]
    [Authorize(Roles = "Admin,HR,Accountant")]
    public async Task<IActionResult> GetPayslip(Guid id, Guid employeeId, [FromQuery] string? lang, CancellationToken ct)
    {
        var pdf = await _payrollService.GetPayslipPdfAsync(id, employeeId, !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase), false, ct);
        return File(pdf, "application/pdf", $"payslip-{id.ToString()[..8]}-{employeeId.ToString()[..8]}.pdf");
    }

    /// <summary>The signed-in employee's own payslip for a published (approved or posted) run.</summary>
    [HttpGet("me/{id:guid}/pdf")]
    public async Task<IActionResult> GetMyPayslip(Guid id, [FromQuery] string? lang, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || !Guid.TryParse(userId, out var applicationUserId))
            return Unauthorized();
        var profile = await _employeeService.GetMyProfileAsync(applicationUserId, ct);
        if (profile is null) return NotFound();

        var pdf = await _payrollService.GetPayslipPdfAsync(id, profile.Id, !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase), true, ct);
        return File(pdf, "application/pdf", $"payslip-{id.ToString()[..8]}.pdf");
    }

    /// <summary>UK companies: the pay run as a spreadsheet for the accountant (the system does not file with HMRC).</summary>
    [HttpGet("{id:guid}/uk-summary")]
    [Authorize(Roles = "Admin,HR,Accountant")]
    public async Task<IActionResult> GetUkSummary(Guid id, CancellationToken ct)
    {
        var csv = await _payrollService.BuildUkSummaryCsvAsync(id, ct);
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"uk-payroll-summary-{id.ToString()[..8]}.csv");
    }

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

    [HttpPost("{id:guid}/revert-to-draft")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PayrollRunDto>> RevertToDraft(Guid id, CancellationToken ct)
        => Ok(await _payrollService.RevertToDraftAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _payrollService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/lines/{employeeId:guid}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PayrollRunDto>> UpdateLine(Guid id, Guid employeeId, UpdatePayrollLineDto dto, CancellationToken ct)
        => Ok(await _payrollService.UpdateLineAsync(id, employeeId, dto, ct));

    [HttpGet("settings")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PayrollSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _payrollService.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PayrollSettingsDto>> UpdateSettings(PayrollSettingsDto dto, CancellationToken ct)
        => Ok(await _payrollService.UpdateSettingsAsync(dto, ct));
}
