using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant,Employee")]
[Route("api/[controller]")]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpGet]
    public async Task<ActionResult<List<WarehouseDto>>> GetAll(CancellationToken ct)
        => Ok(await _warehouseService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<WarehouseDto>> Create(CreateWarehouseDto dto, CancellationToken ct)
        => Ok(await _warehouseService.CreateAsync(dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _warehouseService.DeleteAsync(id, ct);
        return NoContent();
    }
}
