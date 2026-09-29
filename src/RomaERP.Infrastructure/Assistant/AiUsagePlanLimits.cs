namespace RomaERP.Infrastructure.Assistant;

/// <summary>Daily AI-call caps per subscription plan and feature (the "conservative" preset): sized so the
/// worst-case monthly AI cost stays well below what each plan actually bills. Falls back to Essential's
/// (most conservative) limits for a missing or unrecognized plan code — e.g. a trialing tenant with no
/// subscription row yet — so an unmatched tenant never defaults to the most generous cap.</summary>
internal static class AiUsagePlanLimits
{
    private static readonly Dictionary<string, (int BusinessQa, int ExpenseCapture)> ByPlanCode = new()
    {
        ["essential"] = (10, 40),
        ["business"] = (30, 120),
        ["professional"] = (60, 250),
        ["enterprise"] = (150, 600),
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
