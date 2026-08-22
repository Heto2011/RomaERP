using Microsoft.EntityFrameworkCore;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.Persistence.Central;

/// <summary>The one shared database: a registry mapping company codes to their own isolated tenant databases.</summary>
public class CentralDbContext : DbContext
{
    public CentralDbContext(DbContextOptions<CentralDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.Property(t => t.CompanyCode).HasMaxLength(50).IsRequired();
            b.HasIndex(t => t.CompanyCode).IsUnique();
            b.Property(t => t.CompanyNameAr).HasMaxLength(200).IsRequired();
            b.Property(t => t.CompanyNameEn).HasMaxLength(200).IsRequired();
            b.Property(t => t.DatabaseName).HasMaxLength(100).IsRequired();
            b.HasQueryFilter(t => !t.IsDeleted);
        });
    }
}
