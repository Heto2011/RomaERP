using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.Tenancy;

/// <summary>Mutable, request-scoped tenant state. TenantResolutionMiddleware (or the provisioning flow)
/// calls Resolve() before anything else in the scope touches ApplicationDbContext.</summary>
public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string CompanyCode { get; private set; } = string.Empty;
    public string ConnectionString { get; private set; } = string.Empty;
    public Country Country { get; private set; }
    public bool IsResolved { get; private set; }

    public void Resolve(Tenant tenant, string connectionString)
    {
        TenantId = tenant.Id;
        CompanyCode = tenant.CompanyCode;
        ConnectionString = connectionString;
        Country = tenant.Country;
        IsResolved = true;
    }
}
