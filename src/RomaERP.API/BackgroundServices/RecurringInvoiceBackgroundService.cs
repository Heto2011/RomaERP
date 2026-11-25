using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Sales.Services;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.BackgroundServices;

/// <summary>Once a day, prepares every company's recurring invoices that have come due. They only wait for a person to issue or skip them; nothing is invoiced by itself. A period is prepared once, so running it again changes nothing.</summary>
public class RecurringInvoiceBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceProvider _services;
    private readonly ILogger<RecurringInvoiceBackgroundService> _logger;

    public RecurringInvoiceBackgroundService(IServiceProvider services, ILogger<RecurringInvoiceBackgroundService> logger) { _services = services; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await RunForAllTenantsAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogWarning(ex, "Recurring invoice pass failed; will retry next cycle."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunForAllTenantsAsync(CancellationToken ct)
    {
        using var centralScope = _services.CreateScope();
        var central = centralScope.ServiceProvider.GetRequiredService<CentralDbContext>();
        var tenants = await central.Tenants.AsNoTracking().Where(t => t.IsActive && t.DataDeletedAtUtc == null).ToListAsync(ct);
        var today = DateTime.UtcNow.Date;
        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = _services.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Resolve(tenant, registry.BuildConnectionString(tenant.DatabaseName));
                var prepared = await scope.ServiceProvider.GetRequiredService<IRecurringInvoiceService>().PrepareDueAsync(today, ct);
                if (prepared > 0) _logger.LogInformation("Recurring invoices for {Company}: {Prepared} prepared.", tenant.CompanyCode, prepared);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogWarning(ex, "Recurring invoices failed for tenant {TenantId}.", tenant.Id); }
        }
    }
}
