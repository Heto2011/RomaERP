namespace RomaERP.Infrastructure.Assistant;

/// <summary>Daily AI-call caps per subscription plan and feature (the "balanced" preset): generous enough
/// that a legitimate heavy user never feels throttled during normal business use, while worst-case monthly
/// AI cost still stays comfortably below what each plan actually bills. Falls back to Essential's (lowest)
/// limits for a missing or unrecognized plan code — e.g. a trialing tenant with no subscription row yet —
/// so an unmatched tenant never defaults to the most generous cap.</summary>
internal static class AiUsagePlanLimits
{
    private static readonly Dictionary<string, (int BusinessQa, int ExpenseCapture)> ByPlanCode = new()
    {
        // Roma HR has no AI-heavy screens; the small daily numbers are only a burst guard under the monthly cap below.
        ["people"] = (10, 20),
        ["essential"] = (20, 80),
        ["business"] = (50, 200),
        ["professional"] = (100, 400),
        ["enterprise"] = (300, 1000),
    };

    /// <summary>Calls per calendar month (both AI features together), sized so a customer who uses all of it costs us about
    /// 5% of what they pay — at roughly one US cent per call. Enterprise is deliberately absent:
    /// it is sized by agreement with the first real customer of that size, so only the daily limits apply to it.</summary>
    private static readonly Dictionary<string, int> MonthlyCalls = new()
    {
        ["people"] = 100,
        ["essential"] = 150,
        ["business"] = 350,
        ["professional"] = 650,
    };

    public static int? GetMonthly(string? planCode)
        => planCode is not null && MonthlyCalls.TryGetValue(planCode, out var calls) ? calls : null;

    private static readonly (int BusinessQa, int ExpenseCapture) Fallback = ByPlanCode["essential"];

    public static int Get(string? planCode, string featureKey)
    {
        var limits = planCode is not null && ByPlanCode.TryGetValue(planCode, out var found) ? found : Fallback;
        return featureKey switch
        {
            "BusinessQa" => limits.BusinessQa,
            "ExpenseCapture" => limits.ExpenseCapture,
            _ => throw new ArgumentOutOfRangeException(nameof(featureKey), featureKey, "Unknown AI feature key.")
        };
    }
}
