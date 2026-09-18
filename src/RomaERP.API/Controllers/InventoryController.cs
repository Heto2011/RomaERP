using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IItemLotService _lotService;

    public InventoryController(IInventoryService inventoryService, IItemLotService lotService)
    {
        _inventoryService = inventoryService;
        _lotService = lotService;
    }

    [HttpGet("movements")]
    public async Task<ActionResult<List<StockMovementDto>>> GetMovements(CancellationToken ct)
        => Ok(await _inventoryService.GetMovementsAsync(ct));

    [HttpPost("receive")]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<ActionResult<StockMovementDto>> Receive(ReceiveStockDto dto, CancellationToken ct)
        => Ok(await _inventoryService.ReceiveStockAsync(dto, ct));

    [HttpPost("issue")]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<ActionResult<StockMovementDto>> Issue(IssueStockDto dto, CancellationToken ct)
        => Ok(await _inventoryService.IssueStockAsync(dto, ct));

    [HttpGet("lots")]
    public async Task<ActionResult<List<ItemLotDto>>> GetLots(CancellationToken ct)
        => Ok(await _lotService.GetLotsAsync(ct));

    [HttpGet("lots/expiring")]
    public async Task<ActionResult<List<ExpiringLotDto>>> GetExpiringLots([FromQuery] int withinDays, CancellationToken ct)
        => Ok(await _lotService.GetExpiringLotsAsync(withinDays <= 0 ? 7 : withinDays, ct));
}
