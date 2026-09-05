using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RomaERP.API.Middleware;
using RomaERP.API.Services;
using RomaERP.Application;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Persistence.Seed;
using RomaERP.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RomaERP API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل: Bearer {token}"
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!))
        };
    });

builder.Services.AddAuthorization(options =>
{
    // A per-user "module" grant (see ModulePermissions) always passes for Admin and for that module's
    // existing fallback role (Accountant/HR keep working exactly as before), and additionally passes for
    // anyone individually granted that module's claim — letting an Admin hand one specific area to a
    // user without making them a full Accountant/HR.
    foreach (var module in ModulePermissions.All)
    {
        var fallbackRoles = ModulePermissions.FallbackRoles[module];
        options.AddPolicy(ModulePermissions.PolicyName(module), policy => policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            fallbackRoles.Any(ctx.User.IsInRole) ||
            ctx.User.HasClaim(ModulePermissions.ClaimType, module)));
    }
});

// Each self-service trial signup provisions a real, isolated database, so the public endpoint
// gets a per-IP throttle to blunt casual abuse/bots — not a full defense, but a cheap first guard.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("trial-signup", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));

    // A short PIN is brute-forceable, so this throttles guesses per device — a legitimate cashier
    // mistyping a few times in a row never hits it, but a script trying every 4-digit PIN does.
    options.AddPolicy("pos-pin-login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Applies pending EF migrations to the central database and every tenant's own database on every
// startup, in every environment — so a schema change shipped in code is never left stranded on a
// tenant's database after a deploy (unlike demo data seeding below, this must always run).
await MigrateAllTenantsAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    await SeedDemoTenantAsync(app.Services);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();

app.UseMiddleware<TenantClaimConsistencyMiddleware>();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();

// Runs in every environment on every startup: migrates the central database, then migrates every
// existing tenant's own database in turn. A fresh scope per tenant is required since ITenantContext
// can only be resolved once per scope (see TenantContext.Resolve).
static async Task MigrateAllTenantsAsync(IServiceProvider services)
{
    using var centralScope = services.CreateScope();
    var central = centralScope.ServiceProvider.GetRequiredService<CentralDbContext>();
    await central.Database.MigrateAsync();
    await SeedSubscriptionPlansAsync(central);

    var tenants = await central.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync();

    foreach (var tenant in tenants)
    {
        using var tenantScope = services.CreateScope();
        var registry = tenantScope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        var tenantContext = tenantScope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(tenant, registry.BuildConnectionString(tenant.DatabaseName));

        var db = tenantScope.ServiceProvider.GetRequiredService<RomaERP.Infrastructure.Persistence.ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
}

// Seeds the 4 public pricing tiers (marketing/pricing.html) once, on first startup after this feature
// deployed. Safe to call on every startup — only inserts when the table is empty.
static async Task SeedSubscriptionPlansAsync(CentralDbContext central)
{
    if (await central.SubscriptionPlans.AnyAsync()) return;

    central.SubscriptionPlans.AddRange(
        new RomaERP.Domain.Tenancy.SubscriptionPlan { Code = "essential", NameAr = "Essential", NameEn = "Essential", MonthlyBasePrice = 499, IncludedBranches = 1, IncludedUsers = 2, SortOrder = 1 },
        new RomaERP.Domain.Tenancy.SubscriptionPlan { Code = "business", NameAr = "Business", NameEn = "Business", MonthlyBasePrice = 799, IncludedBranches = 3, IncludedUsers = 5, SortOrder = 2 },
        new RomaERP.Domain.Tenancy.SubscriptionPlan { Code = "professional", NameAr = "Professional", NameEn = "Professional", MonthlyBasePrice = 1299, IncludedBranches = 7, IncludedUsers = 10, SortOrder = 3 },
        new RomaERP.Domain.Tenancy.SubscriptionPlan { Code = "enterprise", NameAr = "Enterprise", NameEn = "Enterprise", MonthlyBasePrice = 2499, IncludedBranches = int.MaxValue, IncludedUsers = int.MaxValue, IsCustomPricing = true, SortOrder = 4 }
    );
    await central.SaveChangesAsync();
}

// Creates/seeds the "demo" tenant on startup in Development so the existing dev database keeps working
// unchanged after multi-tenancy was introduced. Manually borrows a scope and resolves its ITenantContext
// to "demo" before touching ApplicationDbContext, since a plain app.Services.CreateScope() would otherwise
// leave the tenant unresolved (see DependencyInjection.AddInfrastructure).
static async Task SeedDemoTenantAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var central = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
    await central.Database.MigrateAsync();

    var demoTenant = await central.Tenants.FirstOrDefaultAsync(t => t.CompanyCode == "demo");
    if (demoTenant is null)
    {
        demoTenant = new RomaERP.Domain.Tenancy.Tenant
        {
            CompanyCode = "demo",
            CompanyNameAr = "شركة تجريبية",
            CompanyNameEn = "Demo Company",
            Country = Country.Egypt,
            DatabaseName = "RomaERP",
            IsActive = true
        };
        central.Tenants.Add(demoTenant);
        await central.SaveChangesAsync();
    }

    var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
    var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
    tenantContext.Resolve(demoTenant, registry.BuildConnectionString(demoTenant.DatabaseName));

    var db = scope.ServiceProvider.GetRequiredService<RomaERP.Infrastructure.Persistence.ApplicationDbContext>();
    await db.Database.MigrateAsync();

    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

public partial class Program
{
}
