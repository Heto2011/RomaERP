using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

/// <summary>Not tenant-scoped — used to create new tenants in the first place, so it's excluded from
/// TenantResolutionMiddleware and protected by a system key instead of a JWT/company code.</summary>
[ApiController]
[Route("api/system")]
[EnableRateLimiting("system-key")]
public class SystemController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioning;
    private readonly IUserTransferService _userTransfer;
    private readonly ISystemPasswordResetService _passwordReset;
    private readonly IConfiguration _configuration;

    public SystemController(
        ITenantProvisioningService provisioning,
        IUserTransferService userTransfer,
        ISystemPasswordResetService passwordReset,
        IConfiguration configuration)
    {
        _provisioning = provisioning;
        _userTransfer = userTransfer;
        _passwordReset = passwordReset;
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

    /// <summary>Internal-only tool for moving a user between companies. Each tenant's database is fully
    /// isolated, so this recreates the person's basic account (email, name, roles) in the target company
    /// and deactivates it in the source — it never carries over history (attendance, leave, payroll) that
    /// belongs to the old company, only the account itself.</summary>
    [HttpPost("users/transfer")]
    public async Task<ActionResult<TransferUserResult>> TransferUser(TransferUserRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _userTransfer.TransferAsync(request, ct));
    }

    /// <summary>Last resort when nobody can log in to a tenant (e.g. its only Admin is locked out) — resets
    /// a user's password directly, bypassing normal auth entirely, gated by the system key alone.</summary>
    [HttpPost("users/reset-password")]
    public async Task<IActionResult> ResetUserPassword(ResetSystemUserPasswordRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        await _passwordReset.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "إنشاء عملاء جدد مش مفعّل — لازم تضيف System:ProvisioningKey في الإعدادات." });

        if (!Request.Headers.TryGetValue("X-System-Key", out var providedKey) || !FixedTimeEquals(providedKey.ToString(), systemKey))
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });

        return null;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
