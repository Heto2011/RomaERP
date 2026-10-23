using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.BackgroundServices;

/// <summary>Once a day, prepares every company's fixed monthly expenses that have come due and tells its Admins
/// (WhatsApp / email) that they are waiting for approval. A month's expense is prepared once, so the notice goes out
/// once per item per month, and running it again (after a restart, say) sends nothing new.</summary>
public class RecurringExpenseReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecurringExpenseReminderBackgroundService> _logger;

    public RecurringExpenseReminderBackgroundService(IServiceProvider services, IConfiguration configuration, ILogger<RecurringExpenseReminderBackgroundService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish starting (and migrations run) before the first pass.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunForAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Fixed-expense reminder pass failed; will retry next cycle.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunForAllTenantsAsync(CancellationToken ct)
    {
        using var centralScope = _services.CreateScope();
        var central = centralScope.ServiceProvider.GetRequiredService<CentralDbContext>();
        var tenants = await central.Tenants.AsNoTracking().Where(t => t.IsActive && t.DataDeletedAtUtc == null).ToListAsync(ct);
        var baseUrl = _configuration["App:PublicBaseUrl"] ?? "https://romagroup.app";
        var today = DateTime.UtcNow.Date;

        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = _services.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Resolve(tenant, registry.BuildConnectionString(tenant.DatabaseName));

                var arabic = tenant.Country is not (Country.UnitedKingdom or Country.Guernsey);
                var outcome = await scope.ServiceProvider.GetRequiredService<IRecurringExpenseReminderService>().RunAsync(today, arabic, baseUrl, ct);
                if (outcome.Prepared > 0)
                    _logger.LogInformation("Fixed expenses for {Company}: {Prepared} prepared, WhatsApp {WhatsApp}, {Emails} email(s).",
                        tenant.CompanyCode, outcome.Prepared, outcome.WhatsAppSent, outcome.EmailsSent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One company's problem (an old database, a bad credential) must not stop the others.
                _logger.LogWarning(ex, "Fixed-expense reminder failed for tenant {TenantId}.", tenant.Id);
            }
        }
    }
}
