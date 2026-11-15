using Microsoft.AspNetCore.Identity;

namespace RomaERP.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Hashed short PIN (set by an Admin) letting this user open the POS quickly without
    /// typing their full email/password — same idea as Foodics' cashier PIN login.</summary>
    public string? PosPinHash { get; set; }

    /// <summary>The branch (warehouse) this user works in. When set, a non-Admin user can only see and act on that branch's stock.
    /// Empty means all branches.</summary>
    public Guid? WarehouseId { get; set; }
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
