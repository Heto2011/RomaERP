using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Tenancy;

public interface ITenantRegistry
{
    Task<Tenant?> FindByCompanyCodeAsync(string companyCode, CancellationToken ct = default);
    string BuildConnectionString(string databaseName);
}

/// <summary>Looks up tenants in the central database and builds the SQL Server connection string
/// for a tenant's own, fully separate database. Results are cached briefly since every request needs one.</summary>
public class TenantRegistry : ITenantRegistry
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly CentralDbContext _central;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public TenantRegistry(CentralDbContext central, IConfiguration configuration, IMemoryCache cache)
    {
        _central = central;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<Tenant?> FindByCompanyCodeAsync(string companyCode, CancellationToken ct = default)
    {
        var cacheKey = $"tenant:{companyCode}";
        if (_cache.TryGetValue(cacheKey, out Tenant? cached))
            return cached;

        var tenant = await _central.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.CompanyCode == companyCode, ct);
        if (tenant is not null)
            _cache.Set(cacheKey, tenant, CacheDuration);

        return tenant;
    }

    public string BuildConnectionString(string databaseName)
    {
        var template = _configuration.GetConnectionString("TenantServer")
            ?? throw new InvalidOperationException("ConnectionStrings:TenantServer غير مُعرّف في الإعدادات.");

        return $"{template}Database={databaseName};";
    }
}
