using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/manufacturing")]
public class ManufacturingController : ControllerBase
{
    private readonly IManufacturingService _manufacturingService;

    public ManufacturingController(IManufacturingService manufacturingService)
    {
        _manufacturingService = manufacturingService;
    }

    [HttpGet("boms")]
    public async Task<ActionResult<List<ManufacturingBomDto>>> GetBoms(CancellationToken ct)
        => Ok(await _manufacturingService.GetBomsAsync(ct));

    [HttpGet("boms/{outputItemId:guid}")]
    public async Task<ActionResult<ManufacturingBomDto>> GetBom(Guid outputItemId, CancellationToken ct)
    {
        var bom = await _manufacturingService.GetBomByOutputItemAsync(outputItemId, ct);
        return bom is null ? NotFound() : Ok(bom);
    }

    [HttpPut("boms/{outputItemId:guid}")]
    public async Task<ActionResult<ManufacturingBomDto>> SetBom(Guid outputItemId, SetManufacturingBomDto dto, CancellationToken ct)
        => Ok(await _manufacturingService.SetBomAsync(outputItemId, dto, ct));

    [HttpDelete("boms/{outputItemId:guid}")]
    public async Task<IActionResult> DeleteBom(Guid outputItemId, CancellationToken ct)
    {
        await _manufacturingService.DeleteBomAsync(outputItemId, ct);
        return NoContent();
    }

    [HttpGet("orders")]
    public async Task<ActionResult<List<ManufacturingOrderDto>>> GetOrders(CancellationToken ct)
        => Ok(await _manufacturingService.GetOrdersAsync(ct));

    [HttpPost("orders")]
    public async Task<ActionResult<ManufacturingOrderDto>> CreateOrder(CreateManufacturingOrderDto dto, CancellationToken ct)
        => Ok(await _manufacturingService.CreateOrderAsync(dto, ct));
}
