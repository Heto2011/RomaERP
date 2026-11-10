using System.Text.Json;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.Middleware;

/// <summary>Stops a company from using an API area its product does not include (for example a free Roma Books company calling
/// the restaurant or HR endpoints), whatever the screens show. Runs after the tenant is known; the full product is never blocked.</summary>
public class ProductScopeGateMiddleware
{
    private readonly RequestDelegate _next;

    public ProductScopeGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (tenant.IsResolved
            && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && !ProductScopeAccess.IsAllowed(tenant.ProductScope, path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var arabic = context.Request.Headers.AcceptLanguage.ToString().StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = arabic
                    ? "الخدمة دي مش ضمن باقتكم. رقّوا الباقة من «اشتراكي» لتفعيلها."
                    : "This feature is not part of your plan. Upgrade from \"My subscription\" to enable it."
            }));
            return;
        }

        await _next(context);
    }
}
