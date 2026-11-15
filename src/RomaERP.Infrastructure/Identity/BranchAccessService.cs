using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Identity;

/// <summary>Reads the signed-in user's branch. An Admin is never limited, and neither is a user whose branch is empty.</summary>
public class BranchAccessService : IBranchAccessService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;
    private bool _loaded;
    private Guid? _branch;

    public BranchAccessService(UserManager<ApplicationUser> users, ICurrentUserService current)
    {
        _users = users;
        _current = current;
    }

    public async Task<Guid?> GetRestrictedBranchAsync(CancellationToken ct = default)
    {
        if (_loaded) return _branch;
        _loaded = true;
        if (!Guid.TryParse(_current.UserId, out var id)) return _branch = null;
        var user = await _users.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user?.WarehouseId is null) return _branch = null;
        if (await _users.IsInRoleAsync(user, "Admin")) return _branch = null;
        return _branch = user.WarehouseId;
    }

    public async Task EnsureMayUseAsync(Guid warehouseId, CancellationToken ct = default)
    {
        var branch = await GetRestrictedBranchAsync(ct);
        if (branch is not null && branch != warehouseId)
            throw new ValidationAppException("حسابك مربوط بفرع تاني. تقدر تتعامل مع فرعك بس.");
    }
}
