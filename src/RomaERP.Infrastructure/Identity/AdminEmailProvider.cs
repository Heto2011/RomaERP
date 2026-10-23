using Microsoft.AspNetCore.Identity;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Identity;

public class AdminEmailProvider : IAdminEmailProvider
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminEmailProvider(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<List<AdminContact>> GetAdminContactsAsync(CancellationToken ct = default)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        return admins
            .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email))
            .Select(u => new AdminContact(u.Email!, u.FullName))
            .ToList();
    }
}
