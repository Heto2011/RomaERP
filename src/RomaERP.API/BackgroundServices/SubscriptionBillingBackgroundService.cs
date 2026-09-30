using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.BackgroundServices;

/// <summary>Runs the monthly billing cycle by itself so no invoice depends on someone remembering to press the
/// button. It only generates invoices for active subscriptions whose period ended (an unpaid invoice never
/// suspends anyone unless Billing:AutoSuspendOverdue is turned on) and logs which tenants are overdue.
/// Safe to run repeatedly: each run advances a subscription's period, so it is invoiced once per month.
/// Turn it off with Billing:AutoRunEnabled=false.</summary>
public class SubscriptionBillingBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionBillingBackgroundService> _logger;

    public SubscriptionBillingBackgroundService(
        IServiceProvider services, IConfiguration configuration, ILogger<SubscriptionBillingBackgroundService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the API finish starting (migrations, seeding) before the first pass.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            if (_configuration.GetValue("Billing:AutoRunEnabled", true))
                await RunOnceAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var billing = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingService>();
            var result = await billing.RunBillingCycleAsync(ct);

            if (result.InvoicesGenerated > 0 || result.Suspended > 0)
                _logger.LogInformation("Billing cycle: {Generated} invoice(s) generated, {Charged} auto-charged, {Suspended} suspended.",
                    result.InvoicesGenerated, result.AutoCharged, result.Suspended);
            foreach (var note in result.Notes)
                _logger.LogWarning("Billing: {Note}", note);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Billing cycle failed; will retry next cycle.");
        }
    }
}
