namespace RomaERP.API.Contracts;

/// <summary>Plain, factual usage counters for the current tenant — no plan/tier logic here, since pricing
/// stays a manual conversation between sales and the customer rather than something the app enforces.</summary>
public record UsageDto(int ActiveUsers, int ActiveBranches, DateTime GeneratedAtUtc);
