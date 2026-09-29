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
    bool SeedDemoData = false,
    ProductScope ProductScope = ProductScope.Full);

public record TenantDto(
    Guid Id,
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    Country Country,
    bool IsActive,
    bool IsDemo,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc,
    ProductScope ProductScope,
    DateTime? DataDeletedAtUtc);

public record DataDeletionRequest(string RequestedByEmail, string? Reason, string ProcessedByEmail);

public record DataDeletionRecordDto(
    Guid Id,
    Guid TenantId,
    string CompanyCode,
    string CompanyNameAr,
    string CompanyNameEn,
    int ConfirmationNumber,
    string RequestedByEmail,
    string? Reason,
    DateTime RequestedAtUtc,
    string ProcessedByEmail,
    DateTime? CompletedAtUtc,
    string? FailureReason);

/// <summary>Creates a brand-new, fully isolated tenant: its own database, schema, chart of accounts, and Admin user.</summary>
public interface ITenantProvisioningService
{
    Task<TenantDto> ProvisionAsync(ProvisionTenantRequest request, CancellationToken ct = default);
    Task<List<TenantDto>> GetTenantsAsync(bool demoOnly, CancellationToken ct = default);

    /// <summary>Deactivates (never deletes) every demo tenant whose ExpiresAtUtc has passed — blocks login
    /// without touching any of the tenant's data, so it can always be reactivated by hand later.</summary>
    Task<int> DeactivateExpiredDemoTenantsAsync(CancellationToken ct = default);

    /// <summary>Honors a customer's official data-deletion request: physically drops the tenant's own SQL
    /// Server database, then permanently marks the tenant record as data-deleted. Always leaves behind a
    /// <see cref="DataDeletionRecordDto"/> proving the request was received and processed (or, if the drop
    /// itself fails, proving it was received and attempted) — that record is never deleted by anything in
    /// this app.</summary>
    Task<DataDeletionRecordDto> ProcessDataDeletionRequestAsync(Guid tenantId, DataDeletionRequest request, CancellationToken ct = default);

    Task<List<DataDeletionRecordDto>> GetDataDeletionRecordsAsync(CancellationToken ct = default);
}
