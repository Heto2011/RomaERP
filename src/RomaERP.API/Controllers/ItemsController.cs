using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
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
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<ItemDto>> Create(CreateItemDto dto, CancellationToken ct)
    {
        var result = await _itemService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _itemService.DeleteAsync(id, ct);
        return NoContent();
    }
}
