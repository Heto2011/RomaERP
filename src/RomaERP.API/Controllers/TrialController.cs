using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.Controllers;

/// <summary>Public, self-service 14-day trial signup — no system key, no sales involvement. Reuses the
/// same real provisioning pipeline as a paid tenant (isolated database, full schema, real chart of
/// accounts), just tagged as a demo/trial so it auto-expires. Not tenant-scoped (see
/// TenantResolutionMiddleware's exempt prefixes), since no tenant exists yet when this runs.</summary>
[ApiController]
[Route("api/trial")]
public class TrialController : ControllerBase
{
    private static readonly Regex NonAlphaNumeric = new("[^a-z0-9]+", RegexOptions.Compiled);

    private readonly ITenantProvisioningService _provisioning;
    private readonly ITenantRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITokenService _tokenService;

    public TrialController(
        ITenantProvisioningService provisioning,
        ITenantRegistry registry,
        IServiceScopeFactory scopeFactory,
        ITokenService tokenService)
    {
        _provisioning = provisioning;
        _registry = registry;
        _scopeFactory = scopeFactory;
        _tokenService = tokenService;
    }

    [HttpPost("signup")]
    [EnableRateLimiting("trial-signup")]
    public async Task<ActionResult<TrialSignupResponse>> SignUp(TrialSignupRequest request, CancellationToken ct)
    {
        var baseSlug = BuildBaseSlug(request.CompanyNameEn, request.AdminEmail);

        TenantDto? tenant = null;
        for (var attempt = 0; attempt < 5 && tenant is null; attempt++)
        {
            var companyCode = attempt == 0 ? baseSlug : $"{baseSlug}-{Random.Shared.Next(1000, 9999)}";
            try
            {
                tenant = await _provisioning.ProvisionAsync(
                    new ProvisionTenantRequest(
                        companyCode,
                        request.CompanyNameAr,
                        request.CompanyNameEn,
                        request.Country,
                        request.AdminEmail,
                        request.AdminPassword,
                        TaxRegistrationNumber: null,
                        IsDemo: true,
                        DemoExpiryDays: 14,
                        SeedDemoData: false),
                    ct);
            }
            catch (ValidationAppException ex) when (ex.Message.Contains("مستخدم قبل كده") && attempt < 4)
            {
                // Company code collision — retry with a random suffix.
            }
        }

        if (tenant is null)
            return BadRequest(new { error = "حصل خطأ أثناء إنشاء الحساب، جرّب اسم شركة مختلف." });

        // Issue a login token immediately so the visitor lands straight in the app.
        using var scope = _scopeFactory.CreateScope();
        var tenantEntity = await _registry.FindByCompanyCodeAsync(tenant.CompanyCode, ct)
            ?? throw new InvalidOperationException("Tenant was just created but could not be re-resolved.");
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenantEntity, _registry.BuildConnectionString(tenantEntity.DatabaseName));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(request.AdminEmail)
            ?? throw new InvalidOperationException("Admin user was just created but could not be found.");

        if (!string.IsNullOrWhiteSpace(request.AdminFullName))
        {
            user.FullName = request.AdminFullName;
            await userManager.UpdateAsync(user);
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateToken(user.Id, user.UserName!, user.Email!, tenant.CompanyCode, roles, Array.Empty<string>());

        return Ok(new TrialSignupResponse(token, tenant.CompanyCode, user.Email!, user.FullName, roles.ToList(), tenant.ExpiresAtUtc));
    }

    private static string BuildBaseSlug(string companyNameEn, string adminEmail)
    {
        var slug = NonAlphaNumeric.Replace(companyNameEn.Trim().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length < 3)
        {
            var emailLocal = NonAlphaNumeric.Replace(adminEmail.Split('@')[0].ToLowerInvariant(), "-").Trim('-');
            slug = emailLocal.Length >= 3 ? emailLocal : "trial";
        }
        return slug.Length > 40 ? slug[..40].Trim('-') : slug;
    }
}
