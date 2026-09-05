using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IItemService _itemService;

    public ItemsController(IItemService itemService)
    {
        _itemService = itemService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ItemDto>>> GetAll(CancellationToken ct)
        => Ok(await _itemService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _itemService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<ActionResult<ItemDto>> Create(CreateItemDto dto, CancellationToken ct)
    {
        var result = await _itemService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<ActionResult<ItemDto>> Update(Guid id, UpdateItemDto dto, CancellationToken ct)
        => Ok(await _itemService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _itemService.DeleteAsync(id, ct);
        return NoContent();
    }
}
