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

builder.Services.AddAuthorization();

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
