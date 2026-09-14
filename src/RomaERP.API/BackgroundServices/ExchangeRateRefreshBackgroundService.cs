using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.Services;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.BackgroundServices;

/// <summary>Keeps every tenant's tracked foreign-currency rates current automatically, so nobody has to
/// remember to type in today's rate — refreshes immediately on startup, then every RefreshInterval. A
/// tenant with no tracked currencies (the common case, single-currency businesses) costs one cheap
/// "distinct currency codes" query and nothing else.</summary>
public class ExchangeRateRefreshBackgroundService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);

    private readonly IServiceProvider _services;
    private readonly ILogger<ExchangeRateRefreshBackgroundService> _logger;

    public ExchangeRateRefreshBackgroundService(IServiceProvider services, ILogger<ExchangeRateRefreshBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            try
            {
                await RefreshAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Best-effort background refresh — a transient failure (network blip, provider down)
                // must never crash the API host. It'll retry on the next tick.
                _logger.LogWarning(ex, "Exchange rate background refresh failed; will retry next cycle.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAllTenantsAsync(CancellationToken ct)
    {
        using var centralScope = _services.CreateScope();
        var central = centralScope.ServiceProvider.GetRequiredService<CentralDbContext>();
        var tenants = await central.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            using var tenantScope = _services.CreateScope();
            var registry = tenantScope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var tenantContext = tenantScope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantContext.Resolve(tenant, registry.BuildConnectionString(tenant.DatabaseName));

            var exchangeRateService = tenantScope.ServiceProvider.GetRequiredService<IExchangeRateService>();
            await exchangeRateService.RefreshAllTrackedCurrenciesAsync(ct);
        }
    }
}
