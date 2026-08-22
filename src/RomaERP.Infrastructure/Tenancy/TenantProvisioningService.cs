using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Persistence;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Persistence.Seed;

namespace RomaERP.Infrastructure.Tenancy;

/// <summary>Creates a brand-new tenant end to end: registers it centrally, creates its own database,
/// applies the schema, and seeds the chart of accounts, roles, Admin user, and company/tax settings.
/// Borrows a fresh DI scope and force-resolves its ITenantContext to the new tenant before touching
/// ApplicationDbContext, so it reuses the exact same DbContext/Identity wiring every other request uses.</summary>
public class TenantProvisioningService : ITenantProvisioningService
{
    private static readonly Regex CompanyCodePattern = new("^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$", RegexOptions.Compiled);

    private readonly CentralDbContext _central;
    private readonly ITenantRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantProvisioningService(CentralDbContext central, ITenantRegistry registry, IServiceScopeFactory scopeFactory)
    {
        _central = central;
        _registry = registry;
        _scopeFactory = scopeFactory;
    }

    public async Task<TenantDto> ProvisionAsync(ProvisionTenantRequest request, CancellationToken ct = default)
    {
        var companyCode = request.CompanyCode.Trim().ToLowerInvariant();
        if (!CompanyCodePattern.IsMatch(companyCode))
            throw new ValidationAppException("كود الشركة لازم يكون حروف إنجليزية صغيرة وأرقام وشرطات بس، من 3 لـ 50 حرف.");

        if (await _central.Tenants.AnyAsync(t => t.CompanyCode == companyCode, ct))
            throw new ValidationAppException("كود الشركة ده مستخدم قبل كده.");

        var databaseName = $"RomaERP_{companyCode.Replace('-', '_')}";
        var tenant = new Tenant
        {
            CompanyCode = companyCode,
            CompanyNameAr = request.CompanyNameAr,
            CompanyNameEn = request.CompanyNameEn,
            Country = request.Country,
            DatabaseName = databaseName,
            IsActive = true
        };

        _central.Tenants.Add(tenant);
        await _central.SaveChangesAsync(ct);

        var connectionString = _registry.BuildConnectionString(databaseName);

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenant, connectionString);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(ct);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DbInitializer.SeedRolesAsync(roleManager);
        await DbInitializer.SeedSingleAdminAsync(userManager, request.AdminEmail, request.AdminPassword);

        await TenantBaselineSeeder.SeedChartOfAccountsAsync(db);
        await TenantBaselineSeeder.SeedFiscalYearAsync(db);
        await TenantBaselineSeeder.SeedCostCenterAsync(db);
        await TenantBaselineSeeder.SeedDepartmentAsync(db);
        await TenantBaselineSeeder.SeedInventoryAsync(db);
        await TenantBaselineSeeder.SeedCompanySettingsAsync(db, request.Country, request.CompanyNameAr, request.CompanyNameEn, request.TaxRegistrationNumber);

        return new TenantDto(tenant.Id, tenant.CompanyCode, tenant.CompanyNameAr, tenant.CompanyNameEn, tenant.Country, tenant.IsActive, tenant.CreatedAtUtc);
    }
}
