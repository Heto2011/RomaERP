using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Common;
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
    private static readonly Regex DatabaseNamePattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

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
            IsActive = true,
            IsDemo = request.IsDemo,
            ExpiresAtUtc = request.IsDemo && request.DemoExpiryDays is { } days ? DateTime.UtcNow.AddDays(days) : null,
            ProductScope = request.ProductScope
        };

        _central.Tenants.Add(tenant);
        _central.TenantActivities.Add(TenantActivityLog.Build(tenant.Id, tenant.CompanyCode, "Company", "Company created", tenant.IsDemo ? "trial" : "paid", "system"));
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

        if (request.ProductScope == ProductScope.PeopleOnly)
        {
            var adminUser = await userManager.FindByEmailAsync(request.AdminEmail);
            if (adminUser is not null)
                await userManager.AddClaimAsync(adminUser, new Claim(ModulePermissions.ClaimType, ModulePermissions.HR));
        }

        await TenantBaselineSeeder.SeedChartOfAccountsAsync(db);
        await TenantBaselineSeeder.SeedFiscalYearAsync(db);
        await TenantBaselineSeeder.SeedCostCenterAsync(db);
        await TenantBaselineSeeder.SeedDepartmentAsync(db);
        await TenantBaselineSeeder.SeedInventoryAsync(db);
        await TenantBaselineSeeder.SeedCompanySettingsAsync(db, request.Country, request.CompanyNameAr, request.CompanyNameEn, request.TaxRegistrationNumber);

        if (request.SeedDemoData)
            await TenantDemoDataSeeder.SeedAsync(scope.ServiceProvider, db, ct);

        return MapTenant(tenant);
    }

    public async Task<List<TenantDto>> GetTenantsAsync(bool demoOnly, CancellationToken ct = default)
    {
        var query = _central.Tenants.AsNoTracking().AsQueryable();
        if (demoOnly)
            query = query.Where(t => t.IsDemo);

        var tenants = await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        return tenants.Select(MapTenant).ToList();
    }

    public async Task<int> DeactivateExpiredDemoTenantsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _central.Tenants
            .Where(t => t.IsDemo && t.IsActive && t.ExpiresAtUtc != null && t.ExpiresAtUtc < now)
            .ToListAsync(ct);

        foreach (var tenant in expired)
        {
            tenant.IsActive = false;
            _central.TenantActivities.Add(TenantActivityLog.Build(tenant.Id, tenant.CompanyCode, "Status", "Trial expired — company locked", "data is kept", "automatic"));
        }

        await _central.SaveChangesAsync(ct);
        return expired.Count;
    }

    public async Task<DataDeletionRecordDto> ProcessDataDeletionRequestAsync(Guid tenantId, DataDeletionRequest request, CancellationToken ct = default)
    {
        var tenant = await _central.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        if (tenant.DataDeletedAtUtc is not null)
            throw new ValidationAppException("بيانات الشركة دي اتمسحت رسميًا قبل كده — مينفعش تتكرر.");

        var record = new DataDeletionRecord
        {
            TenantId = tenant.Id,
            CompanyCode = tenant.CompanyCode,
            CompanyNameAr = tenant.CompanyNameAr,
            CompanyNameEn = tenant.CompanyNameEn,
            RequestedByEmail = request.RequestedByEmail,
            Reason = request.Reason,
            RequestedAtUtc = DateTime.UtcNow,
            ProcessedByEmail = request.ProcessedByEmail,
        };
        _central.DataDeletionRecords.Add(record);
        _central.TenantActivities.Add(TenantActivityLog.Build(tenant.Id, tenant.CompanyCode, "Company", "Data deletion requested", $"ref #{record.ConfirmationNumber}", request.ProcessedByEmail ?? "system console"));
        // Saved before the drop below is even attempted — this proof of the request must exist even if
        // the drop itself fails partway, so support can always show it was received and acted on.
        await _central.SaveChangesAsync(ct);

        if (!DatabaseNamePattern.IsMatch(tenant.DatabaseName))
        {
            record.FailureReason = "اسم قاعدة البيانات غير متوقع — راجع الدعم الفني قبل المتابعة يدويًا.";
            await _central.SaveChangesAsync(ct);
            throw new ValidationAppException($"اتسجل طلب الحذف (مرجع #{record.ConfirmationNumber}) لكن الحذف الفعلي محتاج مراجعة يدوية.");
        }

        try
        {
            await DropTenantDatabaseAsync(tenant.DatabaseName, ct);
        }
        catch (Exception ex)
        {
            record.FailureReason = ex.Message;
            await _central.SaveChangesAsync(ct);
            throw new ValidationAppException($"اتسجل طلب الحذف (مرجع #{record.ConfirmationNumber}) لكن حذف قاعدة البيانات فشل: {ex.Message} — راجع السيرفر يدويًا.");
        }

        tenant.IsActive = false;
        tenant.DataDeletedAtUtc = DateTime.UtcNow;
        record.CompletedAtUtc = tenant.DataDeletedAtUtc;
        await _central.SaveChangesAsync(ct);

        return MapDeletionRecord(record);
    }

    public async Task<DataDeletionRecordDto> DeleteTenantPermanentlyAsync(Guid tenantId, string confirmCompanyCode, string processedBy, CancellationToken ct = default)
    {
        var tenant = await _central.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        if (!string.Equals(confirmCompanyCode?.Trim(), tenant.CompanyCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException("كود الشركة المكتوب مش مطابق — اتلغى الحذف.");

        // Paid invoices are accounting records the business has to keep — never wipe those.
        if (await _central.SubscriptionInvoices.AnyAsync(i => i.TenantId == tenantId && i.Status == SubscriptionInvoiceStatus.Paid, ct))
            throw new ValidationAppException("الشركة دي ليها فواتير مدفوعة — لازم تتحفظ للمحاسبة، فمينفعش تتمسح بالكامل. اعمل لها Suspend بدل ما تمسحها.");

        if (!DatabaseNamePattern.IsMatch(tenant.DatabaseName))
            throw new ValidationAppException("اسم قاعدة البيانات غير متوقع — راجع الدعم الفني قبل الحذف.");

        var record = new DataDeletionRecord
        {
            TenantId = tenant.Id,
            CompanyCode = tenant.CompanyCode,
            CompanyNameAr = tenant.CompanyNameAr,
            CompanyNameEn = tenant.CompanyNameEn,
            RequestedByEmail = processedBy,
            Reason = "Company removed permanently from the system console",
            RequestedAtUtc = DateTime.UtcNow,
            ProcessedByEmail = processedBy,
        };
        _central.DataDeletionRecords.Add(record);
        // Saved first, so a durable trace exists even if the drop below fails.
        await _central.SaveChangesAsync(ct);

        try
        {
            await DropTenantDatabaseAsync(tenant.DatabaseName, ct);
        }
        catch (Exception ex)
        {
            record.FailureReason = ex.Message;
            await _central.SaveChangesAsync(ct);
            throw new ValidationAppException($"حذف قاعدة بيانات الشركة فشل: {ex.Message} — محدش اتمسح من السجلات.");
        }

        _central.SubscriptionInvoices.RemoveRange(_central.SubscriptionInvoices.Where(i => i.TenantId == tenantId));
        _central.Subscriptions.RemoveRange(_central.Subscriptions.Where(s => s.TenantId == tenantId));
        _central.TenantActivities.RemoveRange(_central.TenantActivities.Where(a => a.TenantId == tenantId));
        _central.Tenants.Remove(tenant);
        record.CompletedAtUtc = DateTime.UtcNow;
        await _central.SaveChangesAsync(ct);

        return MapDeletionRecord(record);
    }

    public async Task<List<DataDeletionRecordDto>> GetDataDeletionRecordsAsync(CancellationToken ct = default)
    {
        var records = await _central.DataDeletionRecords.AsNoTracking().OrderByDescending(r => r.RequestedAtUtc).ToListAsync(ct);
        return records.Select(MapDeletionRecord).ToList();
    }

    /// <summary>Connects to the tenant's SQL Server as "master" (a tenant database can't drop itself)
    /// and drops it, kicking out any lingering connections first.</summary>
    private async Task DropTenantDatabaseAsync(string databaseName, CancellationToken ct)
    {
        var masterConnectionString = _registry.BuildConnectionString("master");
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = $@"
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static TenantDto MapTenant(Tenant t) => new(
        t.Id, t.CompanyCode, t.CompanyNameAr, t.CompanyNameEn, t.Country, t.IsActive, t.IsDemo,
        t.ExpiresAtUtc, t.CreatedAtUtc, t.ProductScope, t.DataDeletedAtUtc);

    private static DataDeletionRecordDto MapDeletionRecord(DataDeletionRecord r) => new(
        r.Id, r.TenantId, r.CompanyCode, r.CompanyNameAr, r.CompanyNameEn, r.ConfirmationNumber,
        r.RequestedByEmail, r.Reason, r.RequestedAtUtc, r.ProcessedByEmail, r.CompletedAtUtc, r.FailureReason);
}
