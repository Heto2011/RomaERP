namespace RomaERP.Application.Assistant.Services;

/// <summary>Enforces a per-tenant daily cap on real (paid) Claude API calls for a given feature, so a bug or
/// runaway usage can't rack up unbounded API cost. Each tenant has its own database, so the cap is naturally
/// scoped per company with no tenant id needed. Throws ValidationAppException once the day's cap is hit.</summary>
public interface IAiUsageLimiter
{
    Task EnsureWithinDailyLimitAsync(string featureKey, int dailyLimit, CancellationToken ct = default);
}
