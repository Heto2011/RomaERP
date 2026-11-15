namespace RomaERP.Application.Common.Interfaces;

/// <summary>Which branch (warehouse) the signed-in user is limited to. An Admin, or a user with no branch set, is not limited.</summary>
public interface IBranchAccessService
{
    /// <summary>The branch the user is limited to, or null when they may use every branch.</summary>
    Task<Guid?> GetRestrictedBranchAsync(CancellationToken ct = default);

    /// <summary>Throws when the user is limited to another branch.</summary>
    Task EnsureMayUseAsync(Guid warehouseId, CancellationToken ct = default);
}
