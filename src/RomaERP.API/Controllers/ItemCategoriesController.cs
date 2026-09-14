using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.InventoryPolicy)]
[Route("api/[controller]")]
public class ItemCategoriesController : ControllerBase
{
    private readonly IItemCategoryService _itemCategoryService;

    public ItemCategoriesController(IItemCategoryService itemCategoryService)
    {
        _itemCategoryService = itemCategoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ItemCategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await _itemCategoryService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<ActionResult<ItemCategoryDto>> Create(CreateItemCategoryDto dto, CancellationToken ct)
        => Ok(await _itemCategoryService.CreateAsync(dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ModulePermissions.InventoryPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _itemCategoryService.DeleteAsync(id, ct);
        return NoContent();
    }
}
