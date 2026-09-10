using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/budgets")]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetsController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    [HttpGet("fiscal-years/{fiscalYearId:guid}/lines")]
    public async Task<ActionResult<List<BudgetLineDto>>> GetLines(Guid fiscalYearId, CancellationToken ct)
        => Ok(await _budgetService.GetBudgetLinesAsync(fiscalYearId, ct));

    [HttpPost("lines")]
    public async Task<ActionResult<BudgetLineDto>> SetLine(SetBudgetLineDto dto, CancellationToken ct)
        => Ok(await _budgetService.SetBudgetLineAsync(dto, ct));

    [HttpGet("fiscal-years/{fiscalYearId:guid}/vs-actual")]
    public async Task<ActionResult<BudgetVsActualReportDto>> GetVsActual(Guid fiscalYearId, CancellationToken ct)
        => Ok(await _budgetService.GetBudgetVsActualAsync(fiscalYearId, ct));
}
