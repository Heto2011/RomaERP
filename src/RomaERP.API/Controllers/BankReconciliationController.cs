using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class BankReconciliationController : ControllerBase
{
    private readonly IBankReconciliationService _reconciliationService;

    public BankReconciliationController(IBankReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<BankStatementImportDto>> Import(IFormFile file, [FromForm] Guid bankAccountId, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        await using var stream = file.OpenReadStream();
        return Ok(await _reconciliationService.ImportAsync(stream, file.FileName, bankAccountId, userId, ct));
    }

    [HttpGet("unmatched-lines")]
    public async Task<ActionResult<List<BankStatementLineDto>>> GetUnmatchedLines(CancellationToken ct)
        => Ok(await _reconciliationService.GetUnmatchedLinesAsync(ct));

    [HttpPost("auto-match")]
    public async Task<ActionResult<int>> AutoMatch(CancellationToken ct)
        => Ok(await _reconciliationService.AutoMatchAsync(ct));

    [HttpPost("match")]
    public async Task<ActionResult<ExpenseCaptureDto>> MatchManual(ManualMatchDto dto, CancellationToken ct)
        => Ok(await _reconciliationService.MatchManualAsync(dto, ct));
}
