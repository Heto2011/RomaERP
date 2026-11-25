using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;

namespace RomaERP.API.Controllers;

public record SetActiveRequest(bool Active);

/// <summary>Recurring invoices. Making and issuing them is for the owner and the accountant, because an issued one reaches the books.</summary>
[ApiController]
[Authorize(Policy = ModulePermissions.SalesPolicy)]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/sales/recurring")]
public class RecurringInvoicesController : ControllerBase
{
    private readonly IRecurringInvoiceService _svc;
    private readonly ICurrentUserService _user;
    public RecurringInvoicesController(IRecurringInvoiceService svc, ICurrentUserService user) { _svc = svc; _user = user; }

    [HttpGet] public async Task<ActionResult<List<RecurringInvoiceDto>>> GetAll(CancellationToken ct) => Ok(await _svc.GetAllAsync(ct));
    [HttpPost] public async Task<ActionResult<RecurringInvoiceDto>> Create(SaveRecurringInvoiceDto dto, CancellationToken ct) => Ok(await _svc.SaveAsync(null, dto, ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult<RecurringInvoiceDto>> Update(Guid id, SaveRecurringInvoiceDto dto, CancellationToken ct) => Ok(await _svc.SaveAsync(id, dto, ct));
    [HttpPost("{id:guid}/active")] public async Task<ActionResult<RecurringInvoiceDto>> SetActive(Guid id, SetActiveRequest req, CancellationToken ct) => Ok(await _svc.SetActiveAsync(id, req.Active, ct));
    [HttpGet("pending")] public async Task<ActionResult<List<RecurringDueDto>>> Pending(CancellationToken ct) => Ok(await _svc.GetPendingAsync(ct));
    [HttpPost("pending/{dueId:guid}/issue")] public async Task<ActionResult<RecurringDueDto>> Issue(Guid dueId, IssueRecurringDto dto, CancellationToken ct) => Ok(await _svc.IssueAsync(dueId, dto, _user.UserName, ct));
    [HttpPost("pending/{dueId:guid}/skip")] public async Task<ActionResult<RecurringDueDto>> Skip(Guid dueId, CancellationToken ct) => Ok(await _svc.SkipAsync(dueId, _user.UserName, ct));
    /// <summary>Prepares what is due today without waiting for the daily pass.</summary>
    [HttpPost("prepare-now")] public async Task<ActionResult<int>> PrepareNow(CancellationToken ct) => Ok(await _svc.PrepareDueAsync(DateTime.UtcNow.Date, ct));
}
