namespace RomaERP.Application.Common.Interfaces;

/// <summary>Writes the per-company activity trail the platform owner reads in the system console. Recording must never
/// break the action being recorded, so implementations swallow their own failures.</summary>
public interface ITenantActivityLog
{
    Task RecordAsync(Guid tenantId, string companyCode, string category, string action, string? details = null, string? actor = null, CancellationToken ct = default);

    /// <summary>For code running inside a tenant request: records against the current company.</summary>
    Task RecordForCurrentTenantAsync(string category, string action, string? details = null, string? actor = null, CancellationToken ct = default);
}
