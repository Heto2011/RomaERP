using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;

namespace RomaERP.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    /// <summary>Seeds the demo tenant. Must be called with a service provider from a scope whose
    /// ITenantContext is already resolved (see Program.cs) — this does NOT create its own scope,
    /// since a fresh scope would carry an unresolved tenant and ApplicationDbContext would fail to construct.</summary>
    public static async Task SeedAsync(IServiceProvider scopedServiceProvider)
    {
        var context = scopedServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scopedServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scopedServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await SeedRolesAsync(roleManager);
        await SeedDemoUsersAsync(userManager);
        await TenantBaselineSeeder.SeedChartOfAccountsAsync(context);
        await TenantBaselineSeeder.SeedFiscalYearAsync(context);
        await TenantBaselineSeeder.SeedCostCenterAsync(context);
        await TenantBaselineSeeder.SeedDepartmentAsync(context);
        await TenantBaselineSeeder.SeedInventoryAsync(context);
        await TenantBaselineSeeder.SeedCompanySettingsAsync(context, Country.Egypt, "شركة تجريبية (Demo)", "Demo Company", null);
    }

    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        string[] roles = { "Admin", "Accountant", "HR", "Employee" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    /// <summary>Creates the single Admin user for a freshly provisioned client tenant.</summary>
    public static async Task SeedSingleAdminAsync(UserManager<ApplicationUser> userManager, string email, string password)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ValidationAppException(string.Join("، ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, "Admin");
    }

    private static async Task SeedDemoUsersAsync(UserManager<ApplicationUser> userManager)
    {
        (string Email, string FullName, string Role)[] demoUsers =
        {
            ("admin@romaerp.local", "System Administrator", "Admin"),
            ("accountant@romaerp.local", "محاسب النظام", "Accountant"),
            ("hr@romaerp.local", "مسؤول الموارد البشرية", "HR"),
            ("employee@romaerp.local", "مستخدم موظف", "Employee")
        };

        foreach (var (email, fullName, role) in demoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, "Passw0rd!123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, role);
        }
    }
}
