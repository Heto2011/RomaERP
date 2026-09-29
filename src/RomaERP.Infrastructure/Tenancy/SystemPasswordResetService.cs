using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.Infrastructure.Tenancy;

/// <summary>Resolves the tenant by company code, then resets one user's password directly via
/// UserManager — the same underlying operation as the in-app "Admin resets a user's password" feature,
/// just reachable without being logged in, for when the only Admin account itself is locked out. Same
/// per-tenant DI scope pattern as UserTransferService.</summary>
public class SystemPasswordResetService : ISystemPasswordResetService
{
    private readonly ITenantRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;

    public SystemPasswordResetService(ITenantRegistry registry, IServiceScopeFactory scopeFactory)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
    }

    public async Task ResetPasswordAsync(ResetSystemUserPasswordRequest request, CancellationToken ct = default)
    {
        var companyCode = request.CompanyCode.Trim().ToLowerInvariant();
        var tenant = await _registry.FindByCompanyCodeAsync(companyCode, ct)
            ?? throw new ValidationAppException("الشركة غير موجودة.");

        using var scope = _scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenant, _registry.BuildConnectionString(tenant.DatabaseName));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new NotFoundException(nameof(ApplicationUser), request.Email);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            throw new ValidationAppException(string.Join("، ", result.Errors.Select(e => e.Description)));
    }
}
