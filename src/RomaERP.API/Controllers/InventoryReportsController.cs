using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/[controller]")]
public class InventoryReportsController : ControllerBase
{
    private readonly IInventoryReportService _service;

    public InventoryReportsController(IInventoryReportService service)
    {
        _service = service;
    }

    [HttpGet("stock-valuation")]
    public async Task<ActionResult<StockValuationReportDto>> GetStockValuation(CancellationToken ct)
        => Ok(await _service.GetStockValuationAsync(ct));

    [HttpGet("movement-analysis")]
    public async Task<ActionResult<InventoryMovementReportDto>> GetInventoryMovement(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _service.GetInventoryMovementAsync(fromDate, toDate, ct));

    [HttpGet("purchase-price-variance")]
    public async Task<ActionResult<PurchasePriceVarianceReportDto>> GetPurchasePriceVariance(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _service.GetPurchasePriceVarianceAsync(fromDate, toDate, ct));

    [HttpGet("recipe-cost")]
    public async Task<ActionResult<RecipeCostReportDto>> GetRecipeCost(CancellationToken ct)
        => Ok(await _service.GetRecipeCostAsync(ct));

    [HttpGet("waste-analysis")]
    public async Task<ActionResult<WasteAnalysisReportDto>> GetWasteAnalysis(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _service.GetWasteAnalysisAsync(fromDate, toDate, ct));
}
