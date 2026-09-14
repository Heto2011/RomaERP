using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.HRPolicy)]
[Route("api/work-locations")]
public class WorkLocationsController : ControllerBase
{
    private readonly IWorkLocationService _service;

    public WorkLocationsController(IWorkLocationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkLocationDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<WorkLocationDto>> Create(SaveWorkLocationDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkLocationDto>> Update(Guid id, SaveWorkLocationDto dto, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
