using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddScoped<IUserLanguage, HttpUserLanguage>();
builder.Services.AddHostedService<RomaERP.API.BackgroundServices.ExchangeRateRefreshBackgroundService>();
builder.Services.AddHostedService<RomaERP.API.BackgroundServices.WhatsAppAlertDigestBackgroundService>();
builder.Services.AddHostedService<RomaERP.API.BackgroundServices.DemoTenantExpiryBackgroundService>();
builder.Services.AddHostedService<RomaERP.API.BackgroundServices.SubscriptionBillingBackgroundService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
// The key in the repo's appsettings.json is public, so a server still running with it would accept forged tokens
// for any company. Refuse to start rather than run that way (Development keeps the placeholder for local work).
if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(jwtSection["Key"]) || jwtSection["Key"] == "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_KEY_IN_PRODUCTION_ENV"))
{
    throw new InvalidOperationException("Jwt:Key is not set to a private secret — set it before starting the API.");
}
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
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // The per-IP limit above can be dodged by rotating addresses, so the PIN endpoint ALSO has a cap per company:
    // 4-digit PINs have only 10,000 combinations, and this keeps a distributed guesser from walking all of them.
    // (A second named policy cannot be stacked with an attribute, so this one is applied globally to that path.)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api/auth/pos-pin-login", StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("other");
        return RateLimitPartition.GetFixedWindowLimiter(
            "pin:" + httpContext.Request.Headers["X-Company-Code"].ToString().Trim().ToLowerInvariant(),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });

    // Identity's own lockout only throttles repeated guesses against one account, so this adds a
    // per-IP cap to blunt password spraying across many different tenant accounts from one source.
    // Each request can send an email, so it is capped tighter than login: 5 a minute per IP.
    options.AddPolicy("auth-recovery", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.AddPolicy("auth-login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // The marketing page-view endpoint is open to any anonymous visitor by design, so this just
    // blunts a script hammering it — a real visitor loading a few pages a minute never hits it.
    options.AddPolicy("marketing-pageview", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // Guards the platform-wide system-key endpoints (tenant provisioning, billing console) against
    // brute-forcing the key — a full compromise of these is a cross-tenant compromise.
    options.AddPolicy("system-key", httpContext => RateLimitPartition.GetFixedWindowLimiter(
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

// The API only listens on loopback behind nginx, so without this every request's RemoteIpAddress is
// 127.0.0.1 and the per-IP rate limiters below (trial signup, login, PIN login...) would share ONE
// bucket across all real visitors. Defaults trust forwarded headers only from loopback proxies.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();

app.UseMiddleware<TenantClaimConsistencyMiddleware>();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// Unauthenticated, no tenant/DB dependency — exists purely so an external uptime monitor has
// something cheap to poll to know the API process itself is up and responding.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

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
        await RomaERP.Infrastructure.Persistence.Seed.TenantBaselineSeeder.EnsureFxAccountsAsync(db);
    }
}

// Keeps the 4 public pricing tiers (marketing/roma-erp.html) in the central DB: inserts any missing tier and
// re-aligns the included branches/users and SAR list price of existing ones, so a change to the public page
// only needs the numbers updated here (per-currency prices live in SubscriptionPriceList). Safe on every startup.
static async Task SeedSubscriptionPlansAsync(CentralDbContext central)
{
    var tiers = new (string Code, int Branches, int Users, bool Custom, int Sort)[]
    {
        ("essential", 3, 10, false, 1),
        ("business", 7, 25, false, 2),
        ("professional", 15, 50, false, 3),
        ("enterprise", int.MaxValue, int.MaxValue, true, 4),
        // The standalone HR product: 25 employees included, branches unlimited. Sorted last so it never becomes the
        // default plan of an ERP tenant (the billing service picks it for ROMA People tenants explicitly).
        (RomaERP.Domain.Tenancy.SubscriptionPriceList.PeoplePlanCode, int.MaxValue, RomaERP.Domain.Tenancy.SubscriptionPriceList.PeoplePlanIncludedEmployees, false, 5),
        // The Egypt-only entry plan. Sorted after Roma HR so it is never the default plan of a new trial.
        (RomaERP.Domain.Tenancy.SubscriptionPriceList.MiniPlanCode, RomaERP.Domain.Tenancy.SubscriptionPriceList.MiniPlanIncludedBranches, RomaERP.Domain.Tenancy.SubscriptionPriceList.MiniPlanIncludedUsers, false, 6),
    };

    var existing = await central.SubscriptionPlans.ToListAsync();
    foreach (var (code, branches, users, custom, sort) in tiers)
    {
        var isHr = code == RomaERP.Domain.Tenancy.SubscriptionPriceList.PeoplePlanCode;
        var isMini = code == RomaERP.Domain.Tenancy.SubscriptionPriceList.MiniPlanCode;
        var name = isHr ? "Roma HR" : char.ToUpperInvariant(code[0]) + code[1..];
        // Mini exists only in Egypt, so it has no SAR price; its base price column holds the EGP figure.
        var sarPrice = (isMini
            ? RomaERP.Domain.Tenancy.SubscriptionPriceList.Find(code, RomaERP.Domain.Tenancy.SubscriptionPriceList.Egp)
            : RomaERP.Domain.Tenancy.SubscriptionPriceList.Find(code, RomaERP.Domain.Tenancy.SubscriptionPriceList.Sar))!.Base;
        var plan = existing.FirstOrDefault(p => p.Code == code);
        if (plan is null)
        {
            central.SubscriptionPlans.Add(new RomaERP.Domain.Tenancy.SubscriptionPlan
            {
                Code = code, NameAr = isHr ? "روما إتش آر" : isMini ? "ميني" : name, NameEn = name, MonthlyBasePrice = sarPrice,
                IncludedBranches = branches, IncludedUsers = users, IsCustomPricing = custom, SortOrder = sort
            });
            continue;
        }

        plan.MonthlyBasePrice = sarPrice;
        plan.IncludedBranches = branches;
        plan.IncludedUsers = users;
        plan.IsCustomPricing = custom;
        plan.SortOrder = sort;
    }

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
