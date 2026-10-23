using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RomaERP.Application.Assistant.Services;
using RomaERP.Infrastructure.Persistence;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.Infrastructure.Assistant;

public class AiUsageOverviewService : IAiUsageOverviewService
{
    /// <summary>Rough cost of one AI call (the figure the plan limits are sized with).</summary>
    public const decimal EstimatedUsdPerCall = 0.01m;
    private const int MaxTenants = 500;

    private readonly CentralDbContext _central;
    private readonly ITenantRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiUsageOverviewService> _logger;

    public AiUsageOverviewService(CentralDbContext central, ITenantRegistry registry, IServiceScopeFactory scopeFactory, ILogger<AiUsageOverviewService> logger)
    {
        _central = central;
        _registry = registry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<AiUsageOverviewDto> GetAsync(CancellationToken ct = default)
    {
        var tenants = await _central.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.DataDeletedAtUtc == null)
            .OrderBy(t => t.CompanyCode)
            .Take(MaxTenants)
            .ToListAsync(ct);

        var planByTenant = await _central.Subscriptions.AsNoTracking()
            .Join(_central.SubscriptionPlans, s => s.PlanId, p => p.Id, (s, p) => new { s.TenantId, p.Code })
            .ToListAsync(ct);
        var planCodes = planByTenant.GroupBy(x => x.TenantId).ToDictionary(g => g.Key, g => g.First().Code);

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var rows = new List<TenantAiUsageDto>();

        foreach (var tenant in tenants)
        {
            planCodes.TryGetValue(tenant.Id, out var planCode);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Resolve(tenant, _registry.BuildConnectionString(tenant.DatabaseName));
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var counters = await db.AiUsageCounters.AsNoTracking().Where(c => c.UsageDate >= monthStart).ToListAsync(ct);

                rows.Add(Summarize(
                    tenant.CompanyCode, tenant.CompanyNameEn, planCode, tenant.IsDemo,
                    todayBusinessQa: counters.Where(c => c.UsageDate == today && c.FeatureKey == "BusinessQa").Sum(c => c.Count),
                    todayExpense: counters.Where(c => c.UsageDate == today && c.FeatureKey == "ExpenseCapture").Sum(c => c.Count),
                    monthTotal: counters.Sum(c => c.Count)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read AI usage for {Company}", tenant.CompanyCode);
                rows.Add(new TenantAiUsageDto { CompanyCode = tenant.CompanyCode, CompanyName = tenant.CompanyNameEn, PlanCode = planCode, IsDemo = tenant.IsDemo, Level = "ok", Error = "تعذّر قراءة بيانات الشركة" });
            }
        }

        rows = rows.OrderByDescending(r => r.PercentOfLimit ?? -1).ThenByDescending(r => r.MonthTotal).ToList();
        return new AiUsageOverviewDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            MonthTotalCalls = rows.Sum(r => r.MonthTotal),
            MonthEstimatedCostUsd = rows.Sum(r => r.EstimatedCostUsd),
            TodayTotalCalls = rows.Sum(r => r.TodayBusinessQa + r.TodayExpenseCapture),
            NearOrOverLimit = rows.Count(r => r.Level is "warning" or "over"),
            Tenants = rows
        };
    }

    /// <summary>Turns raw counts into one report row (pure, so it is unit-tested without databases).</summary>
    public static TenantAiUsageDto Summarize(string companyCode, string companyName, string? planCode, bool isDemo, int todayBusinessQa, int todayExpense, int monthTotal)
    {
        var limit = AiUsagePlanLimits.GetMonthly(planCode);
        int? percent = limit is > 0 ? (int)Math.Round(monthTotal * 100.0 / limit.Value) : null;
        var level = percent is null ? "unlimited" : percent >= 100 ? "over" : percent >= 80 ? "warning" : "ok";
        return new TenantAiUsageDto
        {
            CompanyCode = companyCode, CompanyName = companyName, PlanCode = planCode, IsDemo = isDemo,
            TodayBusinessQa = todayBusinessQa, TodayExpenseCapture = todayExpense, MonthTotal = monthTotal,
            MonthlyLimit = limit, PercentOfLimit = percent, Level = level,
            EstimatedCostUsd = monthTotal * EstimatedUsdPerCall
        };
    }
}
