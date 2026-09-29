using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.BackgroundServices;

/// <summary>Automatically locks a demo tenant once its trial expires, so nobody has to remember to press
/// the manual "expire demo tenants" button. Never deletes anything — this only flips IsActive to false,
/// the same soft lock the manual tool already did.</summary>
public class DemoTenantExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IServiceProvider _services;
    private readonly ILogger<DemoTenantExpiryBackgroundService> _logger;

    public DemoTenantExpiryBackgroundService(IServiceProvider services, ILogger<DemoTenantExpiryBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                using var scope = _services.CreateScope();
                var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
                var count = await provisioning.DeactivateExpiredDemoTenantsAsync(stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Demo tenant expiry sweep locked {Count} expired trial(s).", count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Demo tenant expiry sweep failed; will retry next cycle.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
