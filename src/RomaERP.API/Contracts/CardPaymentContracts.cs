namespace RomaERP.API.Contracts;

public record CardPlanOptionDto(string PlanCode, bool Monthly, bool Annual);
public record CardPaymentOptionsDto(bool Enabled, List<CardPlanOptionDto> Plans);
public record StartCheckoutRequest(string PlanCode, bool Annual);
public record CardCheckoutDto(string Url);

public record ExtrasInfoDto(
    bool Enabled, bool IsHr, string PlanCode, string PlanName,
    int IncludedBranches, int PaidBranches, int UsedBranches,
    int IncludedUsers, int PaidUsers, int UsedUsers,
    decimal BranchPrice, decimal UserPrice, bool BranchAvailable, bool UserAvailable,
    List<string> UpgradePlans);
public record ChangeExtrasRequest(string Kind, int Delta);
public record ExtrasChangeResultDto(string? CheckoutUrl, int NewQuantity);
public record UpgradePlanRequest(string PlanCode);
