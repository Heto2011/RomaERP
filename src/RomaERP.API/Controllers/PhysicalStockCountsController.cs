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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
