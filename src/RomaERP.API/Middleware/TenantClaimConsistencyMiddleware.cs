using System.Text.Json;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Middleware;

/// <summary>Defense in depth: once a JWT is validated, its company_code claim (set at login) must match
/// the X-Company-Code header that TenantResolutionMiddleware already resolved for this request. Catches
/// a client bug sending a token from one company alongside the header for another. Must run after
/// UseAuthentication (so context.User is populated) and after TenantResolutionMiddleware.</summary>
public class TenantClaimConsistencyMiddleware
{
    private readonly RequestDelegate _next;

    public TenantClaimConsistencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true && tenantContext.IsResolved)
        {
            var claimCompanyCode = context.User.FindFirst("company_code")?.Value;
            if (!string.IsNullOrEmpty(claimCompanyCode) && !string.Equals(claimCompanyCode, tenantContext.CompanyCode, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "بيانات الدخول غير متطابقة مع الشركة المحددة." }));
                return;
            }
        }

        await _next(context);
    }
}
