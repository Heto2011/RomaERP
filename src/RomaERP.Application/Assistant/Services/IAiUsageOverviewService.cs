namespace RomaERP.Application.Assistant.Services;

public class TenantAiUsageDto
{
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? PlanCode { get; set; }
    public bool IsDemo { get; set; }
    public int TodayBusinessQa { get; set; }
    public int TodayExpenseCapture { get; set; }
    public int MonthTotal { get; set; }

    /// <summary>The plan's monthly cap across both AI features; null when the plan has none (Enterprise, or no plan yet).</summary>
    public int? MonthlyLimit { get; set; }

    /// <summary>Share of the monthly cap already used (0-100+); null when there is no cap.</summary>
    public int? PercentOfLimit { get; set; }

    /// <summary>"ok" | "warning" (80% or more) | "over" (cap reached) | "unlimited".</summary>
    public string Level { get; set; } = "ok";

    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Set instead of numbers when this company's database could not be read.</summary>
    public string? Error { get; set; }
}

public class AiUsageOverviewDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int MonthTotalCalls { get; set; }
    public decimal MonthEstimatedCostUsd { get; set; }
    public int TodayTotalCalls { get; set; }
    public int NearOrOverLimit { get; set; }
    public List<TenantAiUsageDto> Tenants { get; set; } = new();
}

public interface IAiUsageOverviewService
{
    /// <summary>AI usage of every active company this month and today, heaviest first — for the system console.</summary>
    Task<AiUsageOverviewDto> GetAsync(CancellationToken ct = default);
}
