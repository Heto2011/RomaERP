namespace RomaERP.Application.Common.Interfaces;

/// <summary>Keeps a company that pays by card (Lemon Squeezy bills a fixed plan price and cannot charge per extra
/// branch or user) inside what its plan includes. Companies billed by invoice are never blocked — their extras are
/// charged on the invoice — and neither are Roma HR (priced per employee) or negotiated plans.</summary>
public interface IPlanLimitGuard
{
    /// <summary>Throws a validation error when adding one more active branch would exceed the card-paid plan.</summary>
    Task EnsureCanAddBranchAsync(CancellationToken ct = default);

    /// <summary>Throws a validation error when adding (or re-activating) one more user would exceed the card-paid plan.</summary>
    Task EnsureCanAddUserAsync(CancellationToken ct = default);

    /// <summary>Throws when adding one more active employee would exceed a card-paid Roma HR plan plus its paid extras.</summary>
    Task EnsureCanAddEmployeeAsync(CancellationToken ct = default);

    /// <summary>Included, paid-extra and used counts for the "Subscription" page, or null when this company isn't card-paid.</summary>
    Task<PlanUsageDto?> GetUsageAsync(CancellationToken ct = default);
}

public record PlanUsageDto(
    bool IsHr,
    string PlanCode,
    string PlanName,
    int IncludedBranches, int PaidBranches, int UsedBranches,
    int IncludedUsers, int PaidUsers, int UsedUsers);
