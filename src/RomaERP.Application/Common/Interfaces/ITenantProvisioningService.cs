using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common.Interfaces;

public record ProvisionTenantRequest(
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    Country Country,
    string AdminEmail,
    string AdminPassword,
    string? TaxRegistrationNumber,
    bool IsDemo = false,
    int? DemoExpiryDays = null,
    bool SeedDemoData = false);

public record TenantDto(
    Guid Id,
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    Country Country,
    bool IsActive,
    bool IsDemo,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);

/// <summary>Creates a brand-new, fully isolated tenant: its own database, schema, chart of accounts, and Admin user.</summary>
public interface ITenantProvisioningService
{
    Task<TenantDto> ProvisionAsync(ProvisionTenantRequest request, CancellationToken ct = default);
    Task<List<TenantDto>> GetTenantsAsync(bool demoOnly, CancellationToken ct = default);

    /// <summary>Deactivates (never deletes) every demo tenant whose ExpiresAtUtc has passed — blocks login
    /// without touching any of the tenant's data, so it can always be reactivated by hand later.</summary>
    Task<int> DeactivateExpiredDemoTenantsAsync(CancellationToken ct = default);
}
