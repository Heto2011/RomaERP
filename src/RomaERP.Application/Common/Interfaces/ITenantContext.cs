using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.Common.Interfaces;

/// <summary>Read-only view of the tenant resolved for the current request (set by TenantResolutionMiddleware).</summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string CompanyCode { get; }
    string ConnectionString { get; }
    Country Country { get; }
    bool IsResolved { get; }
}
