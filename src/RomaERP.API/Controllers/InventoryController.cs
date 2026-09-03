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

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
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
}
