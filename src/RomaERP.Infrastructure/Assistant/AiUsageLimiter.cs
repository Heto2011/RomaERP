using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Assistant;
using RomaERP.Infrastructure.Persistence;

namespace RomaERP.Infrastructure.Assistant;

public class AiUsageLimiter : IAiUsageLimiter
{
    private readonly ApplicationDbContext _db;

    public AiUsageLimiter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnsureWithinDailyLimitAsync(string featureKey, int dailyLimit, CancellationToken ct = default)
    {
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
