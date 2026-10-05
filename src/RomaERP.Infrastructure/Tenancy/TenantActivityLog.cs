using Microsoft.Extensions.Logging;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Tenancy;

public class TenantActivityLog : ITenantActivityLog
{
    private readonly CentralDbContext _central;
    private readonly ITenantContext _tenant;
    private readonly ILogger<TenantActivityLog> _logger;

    public TenantActivityLog(CentralDbContext central, ITenantContext tenant, ILogger<TenantActivityLog> logger)
    {
        _central = central;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task RecordAsync(Guid tenantId, string companyCode, string category, string action, string? details = null, string? actor = null, CancellationToken ct = default)
    {
        try
        {
            _central.TenantActivities.Add(Build(tenantId, companyCode, category, action, details, actor));
            await _central.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record tenant activity {Category}/{Action}.", category, action);
        }
    }

    public Task RecordForCurrentTenantAsync(string category, string action, string? details = null, string? actor = null, CancellationToken ct = default)
        => _tenant.IsResolved
            ? RecordAsync(_tenant.TenantId, _tenant.CompanyCode, category, action, details, actor, ct)
            : Task.CompletedTask;

    public static TenantActivity Build(Guid tenantId, string companyCode, string category, string action, string? details, string? actor) => new()
    {
        TenantId = tenantId,
        CompanyCode = companyCode,
        Category = category,
        Action = action.Length > 120 ? action[..120] : action,
        Details = details is { Length: > 500 } ? details[..500] : details,
        Actor = actor is { Length: > 200 } ? actor[..200] : actor,
    };
}
