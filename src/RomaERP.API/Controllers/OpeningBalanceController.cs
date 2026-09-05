using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/[controller]")]
public class OpeningBalanceController : ControllerBase
{
    private readonly IOpeningBalanceService _openingBalanceService;

    public OpeningBalanceController(IOpeningBalanceService openingBalanceService)
    {
        _openingBalanceService = openingBalanceService;
    }

    [HttpGet("fiscal-years/{fiscalYearId:guid}")]
    public async Task<ActionResult<JournalEntryDto?>> GetForFiscalYear(Guid fiscalYearId, CancellationToken ct)
        => Ok(await _openingBalanceService.GetForFiscalYearAsync(fiscalYearId, ct));

    [HttpPost]
    public async Task<ActionResult<JournalEntryDto>> Create(CreateOpeningBalanceDto dto, CancellationToken ct)
        => Ok(await _openingBalanceService.CreateAsync(dto, ct));
}
