using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RomaERP.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef migrations`/`database update` tooling. At runtime ApplicationDbContext's
/// connection string comes from the per-request ITenantContext (see DependencyInjection.AddInfrastructure);
/// design-time tooling has no HTTP request to resolve a tenant from, so it needs a fixed connection string
/// just to generate/apply migrations against — the actual database used doesn't matter for schema purposes.</summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        const string designTimeConnectionString =
            "Server=localhost;Database=RomaERP_DesignTime;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(designTimeConnectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
