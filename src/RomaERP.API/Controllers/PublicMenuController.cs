using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.API.Controllers;

public record PublicMenuItemDto(Guid Id, string NameAr, string NameEn, decimal Price, bool OpenPrice);
public record PublicMenuCategoryDto(string Category, List<PublicMenuItemDto> Items);
public record PublicMenuDto(string ShopNameAr, string ShopNameEn, string? Table, List<PublicMenuCategoryDto> Categories, bool CanOrder = false, int RadiusMeters = 0);
public record PublicOrderLineBody(Guid ItemId, decimal Quantity, string? Notes);
public record PublicOrderBody(string Table, string Code, double? Latitude, double? Longitude, double? Accuracy, string? Note, List<PublicOrderLineBody> Lines);

/// <summary>The menu a diner sees after scanning the QR code on a table. No login: the company comes from the X-Company-Code header the
/// public page sends, like every other call. It shows only what a menu board would: names, prices and categories. Nothing about
/// cost, recipes, stock or orders is exposed.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public-menu")]
[EnableRateLimiting("public-menu")]
public class PublicMenuController : ControllerBase
{
    private readonly IRestaurantService _restaurant;
    private readonly ITenantContext _tenant;
    private readonly CentralDbContext _central;
    private readonly IQrOrderingService _qr;

    public PublicMenuController(IRestaurantService restaurant, ITenantContext tenant, CentralDbContext central, IQrOrderingService qr)
    {
        _qr = qr;
        _restaurant = restaurant;
        _tenant = tenant;
        _central = central;
    }

    [HttpGet]
    public async Task<ActionResult<PublicMenuDto>> Get([FromQuery] string? table, [FromQuery] string? k, CancellationToken ct)
    {
        var shop = await _central.Tenants.AsNoTracking().Where(t => t.Id == _tenant.TenantId)
            .Select(t => new { t.CompanyNameAr, t.CompanyNameEn }).FirstOrDefaultAsync(ct);
        var items = await _restaurant.GetMenuAsync(ct);
        var categories = items
            .GroupBy(i => i.CategoryName)
            .OrderBy(g => g.Key)
            .Select(g => new PublicMenuCategoryDto(g.Key, g.OrderBy(i => i.NameAr).Select(i => new PublicMenuItemDto(i.Id, i.NameAr, i.NameEn, i.MenuPrice, i.IsOpenPrice)).ToList()))
            .ToList();
        var tableNumber = string.IsNullOrWhiteSpace(table) ? null : (table.Trim().Length > 20 ? table.Trim()[..20] : table.Trim());
        var canOrder = tableNumber is not null && await _qr.CanOrderAsync(tableNumber, k, ct);
        var radius = canOrder ? (await _qr.GetSettingsAsync(ct)).RadiusMeters : 0;
        return Ok(new PublicMenuDto(shop?.CompanyNameAr ?? string.Empty, shop?.CompanyNameEn ?? string.Empty, tableNumber, categories, canOrder, radius));
    }

    /// <summary>Sends an order from the guest's phone. It is only a request until the cashier approves it.</summary>
    [HttpPost("order")]
    [EnableRateLimiting("public-order")]
    public async Task<ActionResult<object>> Order([FromBody] PublicOrderBody body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = Request.Headers.UserAgent.ToString();
        var deviceKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ip + "|" + ua)));
        var id = await _qr.SubmitAsync(new QrOrderSubmitInput(body.Table, body.Code, body.Latitude, body.Longitude, body.Accuracy, body.Note,
            (body.Lines ?? new()).Select(l => new QrOrderLineInput(l.ItemId, l.Quantity, l.Notes)).ToList()), deviceKey, ct);
        return Ok(new { id });
    }

    /// <summary>Lets the guest's page see whether the cashier accepted the request. The id is a random Guid only that phone received.</summary>
    [HttpGet("order/{id:guid}")]
    public async Task<ActionResult<QrOrderStatusDto>> OrderStatus(Guid id, CancellationToken ct)
    {
        var status = await _qr.GetStatusAsync(id, ct);
        return status is null ? NotFound() : Ok(status);
    }
}
