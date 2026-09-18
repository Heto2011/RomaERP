using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;
using RomaERP.Domain.Accounting;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.ReportsPolicy)]
[Route("api/[controller]")]
public class ManualProfitEntriesController : ControllerBase
{
    private readonly IManualProfitEntryService _service;

    public ManualProfitEntriesController(IManualProfitEntryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ManualProfitEntryDto>>> GetAll([FromQuery] ManualProfitDimension dimension, CancellationToken ct)
        => Ok(await _service.GetAllAsync(dimension, ct));

    [HttpPost]
    public async Task<ActionResult<ManualProfitEntryDto>> Create(CreateManualProfitEntryDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManualProfitEntryDto>> Update(Guid id, UpdateManualProfitEntryDto dto, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
