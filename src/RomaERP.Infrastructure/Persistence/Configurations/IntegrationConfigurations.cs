using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Integration;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class IntegrationApiKeyConfiguration : IEntityTypeConfiguration<IntegrationApiKey>
{
    public void Configure(EntityTypeBuilder<IntegrationApiKey> builder)
    {
        builder.Property(k => k.Name).HasMaxLength(100).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(16).IsRequired();
        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasQueryFilter(k => !k.IsDeleted);
    }
}

public class IntegrationInboxItemConfiguration : IEntityTypeConfiguration<IntegrationInboxItem>
{
    public void Configure(EntityTypeBuilder<IntegrationInboxItem> builder)
    {
        builder.Property(i => i.ExternalRef).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Title).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.Property(i => i.PayloadJson).IsRequired();
        builder.Property(i => i.Source).HasMaxLength(100);
        builder.Property(i => i.ReviewedBy).HasMaxLength(200);
        builder.Property(i => i.ReviewNote).HasMaxLength(500);
        builder.Property(i => i.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(i => new { i.Type, i.ExternalRef }).IsUnique();
        builder.HasIndex(i => i.Status);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
