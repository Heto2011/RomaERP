using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PhysicalStockCountsController : ControllerBase
{
    private readonly IPhysicalStockCountService _service;

    public PhysicalStockCountsController(IPhysicalStockCountService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<PhysicalStockCountDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<PhysicalStockCountDto>> Create(CreatePhysicalStockCountDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));

    [HttpGet("variance-report")]
    public async Task<ActionResult<List<CountVarianceRowDto>>> VarianceReport([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _service.GetVarianceReportAsync(from, to, warehouseId, ct));

    /// <summary>Turns the count's difference into a stock adjustment with its journal entry.</summary>
    [HttpPost("{id:guid}/apply")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<PhysicalStockCountDto>> Apply(Guid id, ApproveCountDto decision, CancellationToken ct)
        => Ok(await _service.ApplyAsync(id, decision, User.Identity?.Name, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<PhysicalStockCountDto>> Reject(Guid id, RejectCountDto dto, CancellationToken ct)
        => Ok(await _service.RejectAsync(id, dto.Note, User.Identity?.Name, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
