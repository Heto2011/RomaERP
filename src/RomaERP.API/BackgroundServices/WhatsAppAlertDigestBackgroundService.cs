using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Notifications.Services;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.BackgroundServices;

/// <summary>Once a day, sends each tenant with WhatsApp notifications enabled a digest of their current
/// Warning/Critical alerts — the automatic half of the feature, alongside the user-triggered "Send via
/// WhatsApp" button. Skips silently (no send, no cost) for a tenant with nothing significant to report, and
/// for any tenant that hasn't configured/enabled WhatsApp at all.</summary>
public class WhatsAppAlertDigestBackgroundService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly ILogger<WhatsAppAlertDigestBackgroundService> _logger;

    public WhatsAppAlertDigestBackgroundService(IServiceProvider services, ILogger<WhatsAppAlertDigestBackgroundService> logger)
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
                await SendDigestToAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Best-effort daily digest — a transient failure (network blip, one tenant's bad credential)
                // must never crash the API host. It'll retry on the next cycle.
                _logger.LogWarning(ex, "WhatsApp alert digest failed; will retry next cycle.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDigestToAllTenantsAsync(CancellationToken ct)
    {
        using var centralScope = _services.CreateScope();
        var central = centralScope.ServiceProvider.GetRequiredService<CentralDbContext>();
        var tenants = await central.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            try
            {
                using var tenantScope = _services.CreateScope();
                var registry = tenantScope.ServiceProvider.GetRequiredService<ITenantRegistry>();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<TenantContext>();
                tenantContext.Resolve(tenant, registry.BuildConnectionString(tenant.DatabaseName));

                var whatsAppService = tenantScope.ServiceProvider.GetRequiredService<IWhatsAppNotificationService>();
                await whatsAppService.SendAlertsDigestAsync(skipIfNothingSignificant: true, ct);
            }
            catch (Exception ex)
            {
                // One tenant's failure (bad token, network blip) must not stop the rest of the run.
                _logger.LogWarning(ex, "WhatsApp alert digest failed for tenant {TenantId}; will retry next cycle.", tenant.Id);
            }
        }
    }
}
