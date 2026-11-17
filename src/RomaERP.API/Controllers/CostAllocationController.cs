using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

/// <summary>Month-end allocation of costs that belong to no branch, on each branch's share of sales. Proposed first; posted only when approved.</summary>
[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/cost-allocation")]
public class CostAllocationController : ControllerBase
{
    private readonly ISharedCostAllocationService _service;
    public CostAllocationController(ISharedCostAllocationService service) => _service = service;

    [HttpGet("preview")]
    public async Task<ActionResult<CostAllocationPreviewDto>> Preview([FromQuery] int year, [FromQuery] int month, CancellationToken ct) => Ok(await _service.PreviewAsync(year, month, ct));

    [HttpPost("apply")]
    public async Task<ActionResult<object>> Apply(ApplyCostAllocationDto dto, CancellationToken ct) => Ok(new { journalEntryId = await _service.ApplyAsync(dto, User.Identity?.Name, ct) });
}
