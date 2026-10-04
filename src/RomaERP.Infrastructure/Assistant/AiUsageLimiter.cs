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
    private readonly IUserLanguage _language;

    public AiUsageLimiter(ApplicationDbContext db, CentralDbContext central, ITenantContext tenantContext, IUserLanguage language)
    {
        _db = db;
        _central = central;
        _tenantContext = tenantContext;
        _language = language;
    }

    public async Task EnsureWithinDailyLimitAsync(string featureKey, CancellationToken ct = default)
    {
        var planCode = await _central.Subscriptions
            .Where(s => s.TenantId == _tenantContext.TenantId)
            .Join(_central.SubscriptionPlans, s => s.PlanId, p => p.Id, (s, p) => p.Code)
            .FirstOrDefaultAsync(ct);

        var dailyLimit = AiUsagePlanLimits.Get(planCode, featureKey);

        var today = DateTime.UtcNow.Date;

        // Monthly cap across both AI features: this is what keeps a heavy user from costing more than their plan earns.
        var monthlyLimit = AiUsagePlanLimits.GetMonthly(planCode);
        if (monthlyLimit is int cap)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var usedThisMonth = await _db.AiUsageCounters
                .Where(c => c.UsageDate >= monthStart)
                .SumAsync(c => (int?)c.Count, ct) ?? 0;
            if (usedThisMonth >= cap)
                throw new ValidationAppException(_language.PrefersArabic
                    ? $"وصلتوا للحد الشهري لاستخدام الذكاء الاصطناعي في باقتكم ({cap} مرة) — يتجدد أول الشهر الجاي."
                    : $"You've reached this month's AI usage limit for your plan ({cap} uses) — it resets at the start of next month.");
        }

        var counter = await _db.AiUsageCounters
            .FirstOrDefaultAsync(c => c.FeatureKey == featureKey && c.UsageDate == today, ct);

        if (counter is null)
        {
            counter = new AiUsageCounter { FeatureKey = featureKey, UsageDate = today, Count = 0 };
            _db.AiUsageCounters.Add(counter);
        }

        if (counter.Count >= dailyLimit)
            throw new ValidationAppException(_language.PrefersArabic
                ? $"وصلتوا للحد الأقصى من استخدام هذه الميزة اليوم ({dailyLimit} مرة) — جربوا تاني بكرة."
                : $"You've reached today's limit for this feature ({dailyLimit} uses) — please try again tomorrow.");

        counter.Count++;
        await _db.SaveChangesAsync(ct);
    }
}
