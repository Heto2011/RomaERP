using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Assistant;
using RomaERP.Infrastructure.Persistence;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.Infrastructure.Assistant;

public class AiUsageLimiter : IAiUsageLimiter
{
    private readonly ApplicationDbContext _db;
    private readonly CentralDbContext _central;
    private readonly ITenantContext _tenantContext;

    public AiUsageLimiter(ApplicationDbContext db, CentralDbContext central, ITenantContext tenantContext)
    {
        _db = db;
        _central = central;
        _tenantContext = tenantContext;
    }

    public async Task EnsureWithinDailyLimitAsync(string featureKey, CancellationToken ct = default)
    {
        var planCode = await _central.Subscriptions
            .Where(s => s.TenantId == _tenantContext.TenantId)
            .Join(_central.SubscriptionPlans, s => s.PlanId, p => p.Id, (s, p) => p.Code)
            .FirstOrDefaultAsync(ct);

        var dailyLimit = AiUsagePlanLimits.Get(planCode, featureKey);

        var today = DateTime.UtcNow.Date;
        var counter = await _db.AiUsageCounters
            .FirstOrDefaultAsync(c => c.FeatureKey == featureKey && c.UsageDate == today, ct);

        if (counter is null)
        {
            counter = new AiUsageCounter { FeatureKey = featureKey, UsageDate = today, Count = 0 };
            _db.AiUsageCounters.Add(counter);
        }

        if (counter.Count >= dailyLimit)
            throw new ValidationAppException($"وصلتوا للحد الأقصى من استخدام هذه الميزة اليوم ({dailyLimit} مرة) — جربوا تاني بكرة.");

        counter.Count++;
        await _db.SaveChangesAsync(ct);
    }
}
