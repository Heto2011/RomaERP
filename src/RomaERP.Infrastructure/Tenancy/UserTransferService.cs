using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.Infrastructure.Tenancy;

/// <summary>Each tenant's database is fully isolated, so "moving" a user is really: read their basic
/// profile from the source tenant's database, create a fresh account for them in the target tenant's
/// database, and (usually) disable the old one. Uses a separate DI scope per tenant, same pattern as
/// TenantProvisioningService, so each side gets its own correctly-resolved TenantContext/DbContext.</summary>
public class UserTransferService : IUserTransferService
{
    private readonly ITenantRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;

    public UserTransferService(ITenantRegistry registry, IServiceScopeFactory scopeFactory)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
    }

    public async Task<TransferUserResult> TransferAsync(TransferUserRequest request, CancellationToken ct = default)
    {
        var sourceCompanyCode = request.SourceCompanyCode.Trim().ToLowerInvariant();
        var targetCompanyCode = request.TargetCompanyCode.Trim().ToLowerInvariant();

        if (sourceCompanyCode == targetCompanyCode)
            throw new ValidationAppException("الشركة المصدر والهدف لازم يكونوا مختلفين.");

        var sourceTenant = await _registry.FindByCompanyCodeAsync(sourceCompanyCode, ct)
            ?? throw new ValidationAppException("الشركة المصدر غير موجودة.");
        var targetTenant = await _registry.FindByCompanyCodeAsync(targetCompanyCode, ct)
            ?? throw new ValidationAppException("الشركة الهدف غير موجودة.");

        string fullName;
        List<string> roles;
        ApplicationUser sourceUser;

        using (var sourceScope = _scopeFactory.CreateScope())
        {
            var sourceTenantContext = sourceScope.ServiceProvider.GetRequiredService<TenantContext>();
            sourceTenantContext.Resolve(sourceTenant, _registry.BuildConnectionString(sourceTenant.DatabaseName));

            var sourceUserManager = sourceScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            sourceUser = await sourceUserManager.FindByEmailAsync(request.Email)
                ?? throw new NotFoundException(nameof(ApplicationUser), request.Email);

            fullName = sourceUser.FullName;
            roles = (await sourceUserManager.GetRolesAsync(sourceUser)).ToList();

            if (request.DeactivateInSource)
            {
                sourceUser.IsActive = false;
                await sourceUserManager.UpdateAsync(sourceUser);
            }
        }

        using (var targetScope = _scopeFactory.CreateScope())
        {
            var targetTenantContext = targetScope.ServiceProvider.GetRequiredService<TenantContext>();
            targetTenantContext.Resolve(targetTenant, _registry.BuildConnectionString(targetTenant.DatabaseName));

            var targetUserManager = targetScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await targetUserManager.FindByEmailAsync(request.Email) is not null)
                throw new ValidationAppException("في مستخدم بنفس الإيميل ده في الشركة الهدف بالفعل.");

            var newUser = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true
            };

            var createResult = await targetUserManager.CreateAsync(newUser, request.NewPassword);
            if (!createResult.Succeeded)
                throw new ValidationAppException(string.Join("، ", createResult.Errors.Select(e => e.Description)));

            if (roles.Count > 0)
                await targetUserManager.AddToRolesAsync(newUser, roles);
        }

        return new TransferUserResult(request.Email, fullName, roles, targetCompanyCode);
    }
}
