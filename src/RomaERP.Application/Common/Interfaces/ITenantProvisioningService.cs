using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common.Interfaces;

public record ProvisionTenantRequest(
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    Country Country,
    string AdminEmail,
    string AdminPassword,
    string? TaxRegistrationNumber);

public record TenantDto(
    Guid Id,
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    Country Country,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Creates a brand-new, fully isolated tenant: its own database, schema, chart of accounts, and Admin user.</summary>
public interface ITenantProvisioningService
{
    Task<TenantDto> ProvisionAsync(ProvisionTenantRequest request, CancellationToken ct = default);
}
