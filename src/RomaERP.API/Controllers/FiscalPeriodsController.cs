using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class FiscalPeriodsController : ControllerBase
{
    private readonly IFiscalPeriodService _fiscalPeriodService;

    public FiscalPeriodsController(IFiscalPeriodService fiscalPeriodService)
    {
        _fiscalPeriodService = fiscalPeriodService;
    }

    [HttpGet("years")]
    public async Task<ActionResult<List<FiscalYearDto>>> GetAllYears(CancellationToken ct)
        => Ok(await _fiscalPeriodService.GetAllYearsAsync(ct));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<FiscalPeriodDto>> ClosePeriod(Guid id, CancellationToken ct)
        => Ok(await _fiscalPeriodService.ClosePeriodAsync(id, ct));

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FiscalPeriodDto>> ReopenPeriod(Guid id, CancellationToken ct)
        => Ok(await _fiscalPeriodService.ReopenPeriodAsync(id, ct));

    [HttpPost("years/{id:guid}/close")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FiscalYearDto>> CloseYear(Guid id, CancellationToken ct)
        => Ok(await _fiscalPeriodService.CloseFiscalYearAsync(id, ct));
}
