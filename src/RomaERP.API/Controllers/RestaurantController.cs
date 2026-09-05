using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Domain.Restaurant;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.POSPolicy)]
[Route("api/restaurant")]
public class RestaurantController : ControllerBase
{
    private readonly IRestaurantService _restaurantService;

    public RestaurantController(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    [HttpGet("tables")]
    public async Task<ActionResult<List<RestaurantTableDto>>> GetTables(CancellationToken ct)
        => Ok(await _restaurantService.GetTablesAsync(ct));

    [HttpPost("tables")]
    public async Task<ActionResult<RestaurantTableDto>> CreateTable(CreateRestaurantTableDto dto, CancellationToken ct)
        => Ok(await _restaurantService.CreateTableAsync(dto, ct));

    public record SetTableStatusRequest(RestaurantTableStatus Status);

    [HttpPut("tables/{id:guid}/status")]
    public async Task<ActionResult<RestaurantTableDto>> SetTableStatus(Guid id, SetTableStatusRequest request, CancellationToken ct)
        => Ok(await _restaurantService.SetTableStatusAsync(id, request.Status, ct));

    [HttpGet("menu")]
    public async Task<ActionResult<List<MenuItemDto>>> GetMenu(CancellationToken ct)
        => Ok(await _restaurantService.GetMenuAsync(ct));

    [HttpGet("menu/{itemId:guid}/recipe")]
    public async Task<ActionResult<List<RecipeLineDto>>> GetRecipe(Guid itemId, CancellationToken ct)
        => Ok(await _restaurantService.GetRecipeAsync(itemId, ct));

    [HttpPut("menu/{itemId:guid}")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> SetMenuItem(Guid itemId, SetMenuItemDto dto, CancellationToken ct)
    {
        await _restaurantService.SetMenuItemAsync(itemId, dto, ct);
        return NoContent();
    }

    [HttpGet("orders")]
    public async Task<ActionResult<List<RestaurantOrderDto>>> GetOrders(bool includeClosed, CancellationToken ct)
        => Ok(await _restaurantService.GetOrdersAsync(includeClosed, ct));

    [HttpGet("orders/{id:guid}")]
    public async Task<ActionResult<RestaurantOrderDto>> GetOrder(Guid id, CancellationToken ct)
        => Ok(await _restaurantService.GetOrderAsync(id, ct));

    [HttpPost("orders")]
    public async Task<ActionResult<RestaurantOrderDto>> CreateOrder(CreateRestaurantOrderDto dto, CancellationToken ct)
        => Ok(await _restaurantService.CreateOrderAsync(dto, ct));

    [HttpPost("orders/{id:guid}/lines")]
    public async Task<ActionResult<RestaurantOrderDto>> AddLine(Guid id, AddOrderLineDto dto, CancellationToken ct)
        => Ok(await _restaurantService.AddLineAsync(id, dto, ct));

    [HttpPut("orders/{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<RestaurantOrderDto>> UpdateLineQuantity(Guid id, Guid lineId, UpdateOrderLineQuantityDto dto, CancellationToken ct)
        => Ok(await _restaurantService.UpdateLineQuantityAsync(id, lineId, dto, ct));

    [HttpDelete("orders/{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<RestaurantOrderDto>> RemoveLine(Guid id, Guid lineId, CancellationToken ct)
        => Ok(await _restaurantService.RemoveLineAsync(id, lineId, ct));

    [HttpPut("orders/{id:guid}/lines/{lineId:guid}/discount")]
    public async Task<ActionResult<RestaurantOrderDto>> SetLineDiscount(Guid id, Guid lineId, SetLineDiscountDto dto, CancellationToken ct)
        => Ok(await _restaurantService.SetLineDiscountAsync(id, lineId, dto, ct));

    [HttpPut("orders/{id:guid}/discount")]
    public async Task<ActionResult<RestaurantOrderDto>> SetOrderDiscount(Guid id, SetOrderDiscountDto dto, CancellationToken ct)
        => Ok(await _restaurantService.SetOrderDiscountAsync(id, dto, ct));

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<ActionResult<RestaurantOrderDto>> CancelOrder(Guid id, CancellationToken ct)
        => Ok(await _restaurantService.CancelOrderAsync(id, ct));

    [HttpPost("orders/{id:guid}/bill")]
    public async Task<ActionResult<RestaurantOrderDto>> BillOrder(Guid id, BillOrderDto dto, CancellationToken ct)
        => Ok(await _restaurantService.BillOrderAsync(id, dto, ct));
}
