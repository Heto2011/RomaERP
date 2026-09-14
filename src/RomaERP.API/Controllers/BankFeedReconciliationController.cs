using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/bank-feed-reconciliation")]
public class BankFeedReconciliationController : ControllerBase
{
    private readonly IBankFeedReconciliationService _service;

    public BankFeedReconciliationController(IBankFeedReconciliationService service)
    {
        _service = service;
    }

    [HttpGet("provider-status")]
    public ActionResult<BankFeedProviderStatusDto> GetProviderStatus()
        => Ok(_service.GetProviderStatus());

    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportBankFeedCsvResultDto>> ImportCsv(IFormFile file, [FromForm] Guid accountId, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");

        await using var stream = file.OpenReadStream();
        return Ok(await _service.ImportCsvAsync(stream, accountId, ct));
    }

    [HttpPost("sync-live")]
    public async Task<ActionResult<SyncLiveBankFeedResultDto>> SyncLive([FromQuery] Guid accountId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _service.SyncLiveAsync(accountId, fromDate, toDate, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<BankFeedReconciliationSummaryDto>> GetSummary([FromQuery] Guid accountId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
        => Ok(await _service.GetSummaryAsync(accountId, fromDate, toDate, ct));

    [HttpPost("auto-match")]
    public async Task<ActionResult<int>> AutoMatch([FromQuery] Guid accountId, CancellationToken ct)
        => Ok(await _service.AutoMatchAsync(accountId, ct));

    [HttpPost("match")]
    public async Task<ActionResult<BankFeedTransactionDto>> MatchManual(ManualMatchBankFeedDto dto, CancellationToken ct)
        => Ok(await _service.MatchManualAsync(dto, ct));

    [HttpPost("{bankFeedTransactionId:guid}/unmatch")]
    public async Task<ActionResult<BankFeedTransactionDto>> Unmatch(Guid bankFeedTransactionId, CancellationToken ct)
        => Ok(await _service.UnmatchAsync(bankFeedTransactionId, ct));
}
