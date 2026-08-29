using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class FinancialReportsController : ControllerBase
{
    private readonly IFinancialReportService _financialReportService;

    public FinancialReportsController(IFinancialReportService financialReportService)
    {
        _financialReportService = financialReportService;
    }

    [HttpGet("income-statement")]
    public async Task<ActionResult<IncomeStatementDto>> GetIncomeStatement(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _financialReportService.GetIncomeStatementAsync(fromDate, toDate, ct));

    [HttpGet("balance-sheet")]
    public async Task<ActionResult<BalanceSheetDto>> GetBalanceSheet([FromQuery] DateTime asOfDate, CancellationToken ct)
        => Ok(await _financialReportService.GetBalanceSheetAsync(asOfDate, ct));

    [HttpGet("cost-center-analysis")]
    public async Task<ActionResult<CostCenterAnalysisDto>> GetCostCenterAnalysis(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _financialReportService.GetCostCenterAnalysisAsync(fromDate, toDate, ct));

    [HttpGet("vat-summary")]
    public async Task<ActionResult<VatSummaryDto>> GetVatSummary(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _financialReportService.GetVatSummaryAsync(fromDate, toDate, ct));

    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowStatementDto>> GetCashFlow(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _financialReportService.GetCashFlowStatementAsync(fromDate, toDate, ct));

    [HttpGet("item-profitability")]
    public async Task<ActionResult<ItemProfitabilityReportDto>> GetItemProfitability(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _financialReportService.GetItemProfitabilityAsync(fromDate, toDate, ct));
}
