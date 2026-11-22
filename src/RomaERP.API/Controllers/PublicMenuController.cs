using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.API.Controllers;

public record PublicMenuItemDto(string NameAr, string NameEn, decimal Price, bool OpenPrice);
public record PublicMenuCategoryDto(string Category, List<PublicMenuItemDto> Items);
public record PublicMenuDto(string ShopNameAr, string ShopNameEn, string? Table, List<PublicMenuCategoryDto> Categories);

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

    public PublicMenuController(IRestaurantService restaurant, ITenantContext tenant, CentralDbContext central)
    {
        _restaurant = restaurant;
        _tenant = tenant;
        _central = central;
    }

    [HttpGet]
    public async Task<ActionResult<PublicMenuDto>> Get([FromQuery] string? table, CancellationToken ct)
    {
        var shop = await _central.Tenants.AsNoTracking().Where(t => t.Id == _tenant.TenantId)
            .Select(t => new { t.CompanyNameAr, t.CompanyNameEn }).FirstOrDefaultAsync(ct);
        var items = await _restaurant.GetMenuAsync(ct);
        var categories = items
            .GroupBy(i => i.CategoryName)
            .OrderBy(g => g.Key)
            .Select(g => new PublicMenuCategoryDto(g.Key, g.OrderBy(i => i.NameAr).Select(i => new PublicMenuItemDto(i.NameAr, i.NameEn, i.MenuPrice, i.IsOpenPrice)).ToList()))
            .ToList();
        var tableNumber = string.IsNullOrWhiteSpace(table) ? null : (table.Trim().Length > 20 ? table.Trim()[..20] : table.Trim());
        return Ok(new PublicMenuDto(shop?.CompanyNameAr ?? string.Empty, shop?.CompanyNameEn ?? string.Empty, tableNumber, categories));
    }
}
