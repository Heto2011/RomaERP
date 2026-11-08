using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/[controller]")]
public class DeferralsController : ControllerBase
{
    private readonly IDeferralService _service;

    public DeferralsController(IDeferralService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeferralDto>>> GetAll(CancellationToken ct) => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<DeferralDto>> Create(CreateDeferralDto dto, CancellationToken ct) => Ok(await _service.CreateAsync(dto, ct));

    [HttpPost("run")]
    public async Task<ActionResult<DeferralRunResultDto>> Run(CancellationToken ct) => Ok(await _service.RunAsync(null, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<DeferralDto>> Cancel(Guid id, CancellationToken ct) => Ok(await _service.CancelAsync(id, ct));
}
