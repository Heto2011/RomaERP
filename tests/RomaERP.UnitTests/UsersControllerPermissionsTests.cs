using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.API.Contracts;
using RomaERP.API.Controllers;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.Services;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>The HR Manager can add and manage ordinary accounts, but must never be able to become (or take over) an
/// Admin — the company owner keeps full control.</summary>
public class UsersControllerPermissionsTests
{
    private sealed class FakeCurrentUser(string? id) : ICurrentUserService
    {
        public string? UserId { get; } = id;
        public string? UserName => "test";
    }

    private static async Task<(ServiceProvider sp, UserManager<ApplicationUser> um)> BuildAsync()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(p => p.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddIdentity<ApplicationUser, ApplicationRole>(o => { o.Password.RequiredLength = 8; o.Password.RequireNonAlphanumeric = false; })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        var sp = services.BuildServiceProvider();
        var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var rm = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var r in new[] { "Admin", "Accountant", "HR", "Employee" })
            await rm.CreateAsync(new ApplicationRole { Name = r });
        return (sp, um);
    }

    private static async Task<ApplicationUser> AddUserAsync(UserManager<ApplicationUser> um, string email, params string[] roles)
    {
        var u = new ApplicationUser { UserName = email, Email = email, FullName = email, EmailConfirmed = true, IsActive = true };
        var res = await um.CreateAsync(u, "Passw0rd123");
        Assert.True(res.Succeeded);
        await um.AddToRolesAsync(u, roles);
        return u;
    }

    private static UsersController ControllerFor(ServiceProvider sp, ApplicationUser caller, string callerRole)
    {
        var c = new UsersController(
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            new FakeCurrentUser(caller.Id.ToString()),
            sp.GetRequiredService<IEmployeeService>(),
            new PasswordHasher<ApplicationUser>());
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, callerRole) }, "test"))
            }
        };
        return c;
    }

    private static int? Status(IActionResult r) => (r as ObjectResult)?.StatusCode ?? (r as StatusCodeResult)?.StatusCode;

    [Fact]
    public async Task HrManager_CanCreateEmployeeAndHrAccounts()
    {
        var (sp, um) = await BuildAsync();
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        var c = ControllerFor(sp, hr, "HR");

        var res = await c.CreateUser(new CreateUserRequest("emp@x.com", "Passw0rd123", "Emp", new List<string> { "Employee" }));
        Assert.IsType<OkObjectResult>(res.Result);
        res = await c.CreateUser(new CreateUserRequest("hr2@x.com", "Passw0rd123", "Hr2", new List<string> { "HR" }));
        Assert.IsType<OkObjectResult>(res.Result);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Accountant")]
    public async Task HrManager_CannotCreatePrivilegedAccounts(string role)
    {
        var (sp, um) = await BuildAsync();
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        var c = ControllerFor(sp, hr, "HR");

        var res = await c.CreateUser(new CreateUserRequest("x@x.com", "Passw0rd123", "X", new List<string> { role }));
        Assert.Equal(403, Status(res.Result!));
        Assert.Null(await um.FindByEmailAsync("x@x.com"));
    }

    [Fact]
    public async Task HrManager_CannotTouchAdminAccount()
    {
        var (sp, um) = await BuildAsync();
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        var admin = await AddUserAsync(um, "ceo@x.com", "Admin");
        var c = ControllerFor(sp, hr, "HR");

        Assert.Equal(403, Status((await c.ResetPassword(admin.Id, new ResetPasswordRequest("NewPassw0rd1"))) ));
        Assert.Equal(403, Status((await c.Deactivate(admin.Id, default)).Result!));
        Assert.Equal(403, Status((await c.UpdateRoles(admin.Id, new UpdateUserRolesRequest(new List<string> { "Employee" }), default)).Result!));
        Assert.True((await um.FindByIdAsync(admin.Id.ToString()))!.IsActive);
    }

    [Fact]
    public async Task HrManager_DoesNotSeeAdminAccountsInList()
    {
        var (sp, um) = await BuildAsync();
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        await AddUserAsync(um, "ceo@x.com", "Admin");
        await AddUserAsync(um, "emp@x.com", "Employee");

        var hrList = ((await ControllerFor(sp, hr, "HR").GetUsers(default)).Result as OkObjectResult)!.Value as List<UserDto>;
        Assert.DoesNotContain(hrList!, u => u.Email == "ceo@x.com");
        Assert.Contains(hrList!, u => u.Email == "emp@x.com");

        var admin = await um.FindByEmailAsync("ceo@x.com");
        var adminList = ((await ControllerFor(sp, admin!, "Admin").GetUsers(default)).Result as OkObjectResult)!.Value as List<UserDto>;
        Assert.Contains(adminList!, u => u.Email == "ceo@x.com");
    }

    [Fact]
    public async Task HrManager_CannotGrantAdminToHimself()
    {
        var (sp, um) = await BuildAsync();
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        var c = ControllerFor(sp, hr, "HR");

        var res = await c.UpdateRoles(hr.Id, new UpdateUserRolesRequest(new List<string> { "Admin" }), default);
        Assert.Equal(403, Status(res.Result!));
        Assert.False(await um.IsInRoleAsync(hr, "Admin"));
    }

    [Fact]
    public async Task HrManager_CannotDeleteOrGrantModules()
    {
        var (sp, um) = await BuildAsync();
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        var emp = await AddUserAsync(um, "emp@x.com", "Employee");
        var c = ControllerFor(sp, hr, "HR");

        Assert.Equal(403, Status(await c.Delete(emp.Id, default)));
        Assert.Equal(403, Status((await c.UpdateModules(emp.Id, new UpdateUserModulesRequest(new List<string> { "Accounting" }), default)).Result!));
    }

    [Fact]
    public async Task Admin_CanDoEverything()
    {
        var (sp, um) = await BuildAsync();
        var admin = await AddUserAsync(um, "ceo@x.com", "Admin");
        var hr = await AddUserAsync(um, "hr@x.com", "HR");
        var c = ControllerFor(sp, admin, "Admin");

        Assert.IsType<OkObjectResult>((await c.CreateUser(new CreateUserRequest("a2@x.com", "Passw0rd123", "A2", new List<string> { "Admin" }))).Result);
        Assert.IsType<NoContentResult>(await c.ResetPassword(hr.Id, new ResetPasswordRequest("NewPassw0rd1")));
        Assert.IsType<OkObjectResult>((await c.UpdateModules(hr.Id, new UpdateUserModulesRequest(new List<string> { "Accounting" }), default)).Result);
        Assert.IsType<NoContentResult>(await c.Delete(hr.Id, default));
    }
}
