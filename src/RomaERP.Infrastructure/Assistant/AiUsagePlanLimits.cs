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
        ["essential"] = (20, 80),
        ["business"] = (50, 200),
        ["professional"] = (100, 400),
        ["enterprise"] = (300, 1000),
    };

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
