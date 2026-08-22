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
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "إنشاء عملاء جدد مش مفعّل — لازم تضيف System:ProvisioningKey في الإعدادات." });

        if (!Request.Headers.TryGetValue("X-System-Key", out var providedKey) || providedKey != systemKey)
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });

        var tenant = await _provisioning.ProvisionAsync(request, ct);
        return Ok(tenant);
    }
}
