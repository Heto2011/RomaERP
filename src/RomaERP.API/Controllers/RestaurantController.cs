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
    private readonly IMenuImportService _menuImport;

    public RestaurantController(IRestaurantService restaurantService, IMenuImportService menuImport)
    {
        _restaurantService = restaurantService;
        _menuImport = menuImport;
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

    /// <summary>Loads a customer's existing menu in bulk. Apply = false only previews the result.</summary>
    [HttpPost("menu/import")]
    [Authorize(Roles = "Admin,Accountant")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<MenuImportResultDto>> ImportMenu(MenuImportRequestDto request, CancellationToken ct)
        => Ok(await _menuImport.ImportAsync(request, ct));

    /// <summary>Reads a menu from a PDF or photo (AI) into import rows. Saves nothing.</summary>
    [HttpPost("menu/scan")]
    [Authorize(Roles = "Admin,Accountant")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<MenuScanResultDto>> ScanMenu(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new RomaERP.Application.Common.Exceptions.ValidationAppException("الملف المرفوع فارغ.");

        var mediaType = file.ContentType?.ToLowerInvariant() switch
        {
            "application/pdf" => "application/pdf",
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            _ => throw new RomaERP.Application.Common.Exceptions.ValidationAppException("الملف لازم يكون PDF أو صورة (JPEG / PNG / WEBP).")
        };

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, ct);
        var bytes = memoryStream.ToArray();
        if (mediaType == "application/pdf" && !(bytes.Length > 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46))
            throw new RomaERP.Application.Common.Exceptions.ValidationAppException("الملف مش PDF سليم.");
        return Ok(await _menuImport.ScanAsync(bytes, mediaType, ct));
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

    [HttpPut("orders/{id:guid}/lines/{lineId:guid}/kitchen-status")]
    public async Task<ActionResult<RestaurantOrderDto>> SetLineKitchenStatus(Guid id, Guid lineId, SetLineKitchenStatusDto dto, CancellationToken ct)
        => Ok(await _restaurantService.SetLineKitchenStatusAsync(id, lineId, dto, ct));

    [HttpPost("orders/{id:guid}/split")]
    public async Task<ActionResult<SplitOrderResultDto>> SplitOrder(Guid id, SplitOrderDto dto, CancellationToken ct)
        => Ok(await _restaurantService.SplitOrderAsync(id, dto, ct));

    /// <summary>Receives the tickets a POS terminal rang up while it had no internet. Returns one result per ticket; failures don't stop the others.</summary>
    [HttpPost("orders/offline-sync")]
    public async Task<ActionResult<List<OfflineOrderResultDto>>> SyncOfflineOrders(OfflineOrdersBatchDto batch, CancellationToken ct)
        => Ok(await _restaurantService.SyncOfflineOrdersAsync(batch, ct));

    [HttpPut("orders/{id:guid}/loyalty")]
    public async Task<ActionResult<RestaurantOrderDto>> SetOrderLoyalty(Guid id, SetOrderLoyaltyDto dto, CancellationToken ct)
        => Ok(await _restaurantService.SetOrderLoyaltyAsync(id, dto, ct));

    [HttpDelete("orders/{id:guid}/loyalty")]
    public async Task<ActionResult<RestaurantOrderDto>> ClearOrderLoyalty(Guid id, CancellationToken ct)
        => Ok(await _restaurantService.ClearOrderLoyaltyAsync(id, ct));

    [HttpPost("orders/{id:guid}/move-table")]
    public async Task<ActionResult<RestaurantOrderDto>> MoveOrderToTable(Guid id, MoveOrderTableDto dto, CancellationToken ct)
        => Ok(await _restaurantService.MoveOrderToTableAsync(id, dto, ct));

    [HttpPost("orders/{id:guid}/merge")]
    public async Task<ActionResult<RestaurantOrderDto>> MergeOrders(Guid id, MergeOrdersDto dto, CancellationToken ct)
        => Ok(await _restaurantService.MergeOrdersAsync(id, dto, ct));

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<ActionResult<RestaurantOrderDto>> CancelOrder(Guid id, CancellationToken ct)
        => Ok(await _restaurantService.CancelOrderAsync(id, ct));

    [HttpPost("orders/{id:guid}/bill")]
    public async Task<ActionResult<RestaurantOrderDto>> BillOrder(Guid id, BillOrderDto dto, CancellationToken ct)
        => Ok(await _restaurantService.BillOrderAsync(id, dto, ct));

    [HttpPost("orders/{id:guid}/void")]
    public async Task<ActionResult<RestaurantOrderDto>> VoidOrder(Guid id, VoidOrderDto dto, CancellationToken ct)
        => Ok(await _restaurantService.VoidOrderAsync(id, dto, ct));
}
