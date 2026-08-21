using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
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
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<ItemCategoryDto>> Create(CreateItemCategoryDto dto, CancellationToken ct)
        => Ok(await _itemCategoryService.CreateAsync(dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _itemCategoryService.DeleteAsync(id, ct);
        return NoContent();
    }
}
