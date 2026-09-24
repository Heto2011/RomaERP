using System.Text.Json;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.Middleware;

/// <summary>Every API request (except tenant provisioning itself) must identify its tenant. Almost all of
/// them do that via an X-Company-Code header; this resolves it against the central tenant registry and
/// points the request's ApplicationDbContext at that tenant's own, fully separate database. Must run
/// before Authentication/MapControllers, since login itself needs the tenant resolved before it can look
/// up users.</summary>
public class TenantResolutionMiddleware
{
    private static readonly string[] ExemptPrefixes = { "/api/system", "/api/trial", "/api/health", "/swagger", "/api/marketing" };
    private const string DeliveryWebhooksPrefix = "/api/delivery-webhooks/";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantRegistry tenantRegistry, TenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || ExemptPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // The caller here is an external delivery platform, not our own frontend — it can't be trusted to
        // (or expected to) send our internal X-Company-Code header, and a client-controllable header would
        // let a forged webhook target an arbitrary tenant anyway. Each tenant instead gets its own webhook
        // URL with its company code baked into the path (see DeliveryWebhooksController), and the actual
        // per-tenant webhook secret — not this route segment — is what a forged/replayed request would
        // still have to defeat.
        string companyCode;
        if (path.StartsWith(DeliveryWebhooksPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = path[DeliveryWebhooksPrefix.Length..];
            var segmentEnd = remainder.IndexOf('/');
            companyCode = (segmentEnd < 0 ? remainder : remainder[..segmentEnd]).Trim().ToLowerInvariant();
            if (companyCode.Length == 0)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "رابط الويب هوك غير صحيح.");
                return;
            }
        }
        else
        {
            if (!context.Request.Headers.TryGetValue("X-Company-Code", out var values) || string.IsNullOrWhiteSpace(values.ToString()))
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "لازم تحدد كود الشركة (X-Company-Code).");
                return;
            }

            companyCode = values.ToString().Trim().ToLowerInvariant();
        }

        var tenant = await tenantRegistry.FindByCompanyCodeAsync(companyCode, context.RequestAborted);
        if (tenant is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "الشركة غير موجودة.");
            return;
        }

        if (!tenant.IsActive)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "الاشتراك متوقف، تواصل مع الدعم.");
            return;
        }

        tenantContext.Resolve(tenant, tenantRegistry.BuildConnectionString(tenant.DatabaseName));

        await _next(context);
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
