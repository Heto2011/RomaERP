using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Infrastructure.Assistant;
using RomaERP.Infrastructure.EInvoicing.Zatca;
using RomaERP.Infrastructure.Identity;
using RomaERP.Infrastructure.Persistence;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Security;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IZatcaDocumentSigner, ZatcaXadesDocumentSigner>();

        services.AddDbContext<CentralDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Central")));

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantRegistry, TenantRegistry>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        // Every tenant has its own, fully separate database. The connection string is only known once
        // TenantResolutionMiddleware resolves the request's company code, so it's read lazily here from
        // the scoped ITenantContext rather than a single fixed configuration value. `dotnet ef` tooling
        // probes every registered DbContext through this same DI path with no request/tenant in scope —
        // it only needs a syntactically valid connection string to build the model, never an unresolved
        // exception, so it falls back to a placeholder that's never actually reached by a real request
        // (TenantResolutionMiddleware always resolves the tenant before any request touches this context).
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            var tenantContext = provider.GetRequiredService<ITenantContext>();
            var connectionString = tenantContext.IsResolved
                ? tenantContext.ConnectionString
                : "Server=localhost;Database=RomaERP_DesignTime;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;";

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();

        services.Configure<ClaudeSettings>(configuration.GetSection(ClaudeSettings.SectionName));
        services.AddHttpClient<IClaudeExpenseParser, ClaudeExpenseParser>();

        return services;
    }
}
