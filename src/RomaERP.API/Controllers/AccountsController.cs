using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IChartImportService _chartImport;
    private readonly IAccountBalanceTransferService _transfer;

    public AccountsController(IAccountService accountService, IChartImportService chartImport, IAccountBalanceTransferService transfer)
    {
        _transfer = transfer;
        _accountService = accountService;
        _chartImport = chartImport;
    }

    // ----- Replace the chart of accounts from an Excel/CSV file: read -> edit in the preview -> approve. -----

    [HttpPost("import/preview")]
    [Authorize(Roles = "Admin,Accountant")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ChartImportPreviewDto>> ImportPreview(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "اختار ملف الأول." });
        await using var stream = file.OpenReadStream();
        return Ok(await _chartImport.PreviewFileAsync(stream, file.FileName, ct));
    }

    [HttpPost("import/validate")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<ChartImportPreviewDto>> ImportValidate(ChartImportApplyRequest request, CancellationToken ct)
        => Ok(await _chartImport.PreviewRowsAsync(request.Rows, ct));

    [HttpPost("import/apply")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<ChartImportResultDto>> ImportApply(ChartImportApplyRequest request, CancellationToken ct)
        => Ok(await _chartImport.ApplyAsync(request.Rows, ct));

    [HttpGet("import/template")]
    public IActionResult ImportTemplate()
        => File(_chartImport.BuildTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "chart-of-accounts-template.xlsx");

    // ----- Move an old account's balance to a new one (after the chart was replaced): preview, then approve. -----

    [HttpPost("transfer-balance/preview")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<BalanceTransferPreviewDto>> TransferPreview(BalanceTransferRequestDto request, CancellationToken ct)
        => Ok(await _transfer.PreviewAsync(request, ct));

    [HttpPost("transfer-balance/apply")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<BalanceTransferResultDto>> TransferApply(BalanceTransferRequestDto request, CancellationToken ct)
        => Ok(await _transfer.ApplyAsync(request, ct));

    [HttpGet("tree")]
    public async Task<ActionResult<List<AccountDto>>> GetTree(CancellationToken ct)
        => Ok(await _accountService.GetTreeAsync(ct));

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll(CancellationToken ct)
        => Ok(await _accountService.GetFlatListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _accountService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.AccountingPolicy)]
    public async Task<ActionResult<AccountDto>> Create(CreateAccountDto dto, CancellationToken ct)
    {
        var result = await _accountService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ModulePermissions.AccountingPolicy)]
    public async Task<ActionResult<AccountDto>> Update(Guid id, UpdateAccountDto dto, CancellationToken ct)
        => Ok(await _accountService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ModulePermissions.AccountingPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _accountService.DeleteAsync(id, ct);
        return NoContent();
    }
}
