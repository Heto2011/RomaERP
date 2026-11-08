using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Application.Common;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.AccountingPolicy)]
[Route("api/[controller]")]
public class DailySalesController : ControllerBase
{
    private readonly IDailySalesService _service;

    public DailySalesController(IDailySalesService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<DailySalesEntryDto>>> GetRecent([FromQuery] int take = 30, CancellationToken ct = default)
        => Ok(await _service.GetRecentAsync(take, ct));

    [HttpPost]
    public async Task<ActionResult<DailySalesEntryDto>> Create(CreateDailySalesDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));
}
