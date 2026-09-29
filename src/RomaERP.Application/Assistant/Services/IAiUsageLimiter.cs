namespace RomaERP.Application.Assistant.Services;

/// <summary>Enforces a per-tenant daily cap on real (paid) Claude API calls, sized by the tenant's current
/// subscription plan (see AiUsagePlanLimits in Infrastructure) so a stuck loop or a bored user can't rack up
/// unbounded cost — and so a cheaper plan can't quietly absorb an Enterprise-sized AI bill.</summary>
public interface IAiUsageLimiter
{
    Task EnsureWithinDailyLimitAsync(string featureKey, CancellationToken ct = default);
}
