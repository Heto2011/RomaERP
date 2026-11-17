using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

/// <summary>Read-only review tools for the accountant and the owner.</summary>
[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/review")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _service;
    public ReviewController(IReviewService service) => _service = service;

    [HttpGet("reconciliation")]
    public async Task<ActionResult<List<ReconciliationCheckDto>>> Reconciliation(CancellationToken ct) => Ok(await _service.GetReconciliationAsync(ct));

    [HttpGet("close-checklist")]
    public async Task<ActionResult<List<CloseCheckDto>>> CloseChecklist([FromQuery] int year, [FromQuery] int month, CancellationToken ct) => Ok(await _service.GetCloseChecklistAsync(year, month, ct));

    [HttpGet("approvals")]
    public async Task<ActionResult<List<ApprovalLogDto>>> Approvals(CancellationToken ct) => Ok(await _service.GetApprovalsLogAsync(ct));

    [HttpGet("unusual-entries")]
    public async Task<ActionResult<List<UnusualEntryDto>>> Unusual([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Ok(await _service.GetUnusualEntriesAsync(from, to, ct));

    [HttpGet("number-gaps")]
    public async Task<ActionResult<List<NumberGapDto>>> NumberGaps(CancellationToken ct) => Ok(await _service.GetNumberGapsAsync(ct));
}
