using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class DeliveryReconciliationController : ControllerBase
{
    private readonly IDeliveryReconciliationService _service;

    public DeliveryReconciliationController(IDeliveryReconciliationService service)
    {
        _service = service;
    }

    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DeliverySettlementImportDto>> Import(IFormFile file, [FromForm] string platformName, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        await using var stream = file.OpenReadStream();
        return Ok(await _service.ImportAsync(stream, file.FileName, platformName, userId, ct));
    }

    [HttpGet("imports")]
    public async Task<ActionResult<List<DeliverySettlementImportDto>>> GetImports(CancellationToken ct)
        => Ok(await _service.GetImportsAsync(ct));

    [HttpGet("reconciliation")]
    public async Task<ActionResult<DeliveryReconciliationReportDto>> GetReconciliation(
        [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _service.GetReconciliationAsync(fromDate, toDate, ct));
}
