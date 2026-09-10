using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

/// <summary>Not tenant-scoped — used to create new tenants in the first place, so it's excluded from
/// TenantResolutionMiddleware and protected by a system key instead of a JWT/company code.</summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioning;
    private readonly IConfiguration _configuration;

    public SystemController(ITenantProvisioningService provisioning, IConfiguration configuration)
    {
        _provisioning = provisioning;
        _configuration = configuration;
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<TenantDto>> CreateTenant(ProvisionTenantRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var tenant = await _provisioning.ProvisionAsync(request, ct);
        return Ok(tenant);
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<List<TenantDto>>> GetTenants([FromQuery] bool demoOnly, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _provisioning.GetTenantsAsync(demoOnly, ct));
    }

    [HttpPost("tenants/expire-demo")]
    public async Task<ActionResult<object>> ExpireDemoTenants(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var count = await _provisioning.DeactivateExpiredDemoTenantsAsync(ct);
        return Ok(new { deactivatedCount = count });
    }

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "إنشاء عملاء جدد مش مفعّل — لازم تضيف System:ProvisioningKey في الإعدادات." });

        if (!Request.Headers.TryGetValue("X-System-Key", out var providedKey) || providedKey != systemKey)
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });

        return null;
    }
}
