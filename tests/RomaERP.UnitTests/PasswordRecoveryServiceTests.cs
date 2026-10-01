using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PasswordRecoveryServiceTests
{
    private sealed class FakeEmail(bool configured = true) : IEmailSender
    {
        public bool IsConfigured { get; } = configured;
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((to, subject, htmlBody));
            return Task.FromResult(new EmailSendResult(true, null));
        }
    }

    private sealed class FakeTenant(ProductScope scope = ProductScope.Full) : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string CompanyCode => "acme";
        public string ConnectionString => "";
        public Country Country => Country.SaudiArabia;
        public ProductScope ProductScope { get; } = scope;
        public bool IsResolved => true;
    }

    private static async Task<(PasswordRecoveryService svc, UserManager<ApplicationUser> um, FakeEmail email)> BuildAsync(
        bool configured = true, ProductScope scope = ProductScope.Full)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddIdentity<ApplicationUser, ApplicationRole>(o => { o.Password.RequiredLength = 8; o.Password.RequireNonAlphanumeric = false; })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        var sp = services.BuildServiceProvider();
        var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "a@x.com", Email = "a@x.com", FullName = "Aisha", EmailConfirmed = true, IsActive = true };
        Assert.True((await um.CreateAsync(user, "OldPassw0rd1")).Succeeded);
        var email = new FakeEmail(configured);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:PublicBaseUrl"] = "https://example.test" }).Build();
        return (new PasswordRecoveryService(um, email, new FakeTenant(scope), cfg, NullLogger<PasswordRecoveryService>.Instance), um, email);
    }

    private static string TokenFrom(string html)
    {
        var start = html.IndexOf("t=", StringComparison.Ordinal) + 2;
        var end = html.IndexOfAny(new[] { '&', '"' }, start);
        return Uri.UnescapeDataString(System.Net.WebUtility.HtmlDecode(html[start..end]));
    }

    [Fact]
    public async Task Request_ForKnownUser_EmailsAResetLinkForThatCompany()
    {
        var (svc, _, email) = await BuildAsync();
        await svc.RequestResetAsync("a@x.com");

        var sent = Assert.Single(email.Sent);
        Assert.Equal("a@x.com", sent.To);
        Assert.Contains("https://example.test/reset-password?c=acme&amp;e=a%40x.com&amp;t=", sent.Html);
    }

    [Fact]
    public async Task Request_ForPeopleOnlyTenant_SendsThemBackToThePeoplePortal()
    {
        var (svc, _, email) = await BuildAsync(scope: ProductScope.PeopleOnly);
        await svc.RequestResetAsync("a@x.com");
        Assert.Contains("p=people", email.Sent.Single().Html);
    }

    [Fact]
    public async Task Request_ForUnknownOrInactiveUser_SendsNothingAndDoesNotThrow()
    {
        var (svc, um, email) = await BuildAsync();
        await svc.RequestResetAsync("nobody@x.com");
        var user = (await um.FindByEmailAsync("a@x.com"))!;
        user.IsActive = false;
        await um.UpdateAsync(user);
        await svc.RequestResetAsync("a@x.com");
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Request_WhenNoEmailProviderIsConfigured_DoesNothing()
    {
        var (svc, _, email) = await BuildAsync(configured: false);
        await svc.RequestResetAsync("a@x.com");
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Reset_WithTheEmailedToken_ChangesThePassword_AndTheTokenWorksOnlyOnce()
    {
        var (svc, um, email) = await BuildAsync();
        await svc.RequestResetAsync("a@x.com");
        var token = TokenFrom(email.Sent.Single().Html);

        Assert.Null(await svc.ResetAsync("a@x.com", token, "NewPassw0rd9"));
        var user = (await um.FindByEmailAsync("a@x.com"))!;
        Assert.True(await um.CheckPasswordAsync(user, "NewPassw0rd9"));
        Assert.False(await um.CheckPasswordAsync(user, "OldPassw0rd1"));

        Assert.NotNull(await svc.ResetAsync("a@x.com", token, "AnotherPassw0rd"));
    }

    [Fact]
    public async Task Reset_WithAWrongTokenOrWeakPassword_IsRejected()
    {
        var (svc, um, email) = await BuildAsync();
        await svc.RequestResetAsync("a@x.com");
        var token = TokenFrom(email.Sent.Single().Html);

        Assert.NotNull(await svc.ResetAsync("a@x.com", "garbage", "NewPassw0rd9"));
        Assert.NotNull(await svc.ResetAsync("a@x.com", token, "short"));
        Assert.True(await um.CheckPasswordAsync((await um.FindByEmailAsync("a@x.com"))!, "OldPassw0rd1"));
    }
}
