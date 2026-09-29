using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Assistant;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class AiUsageCounterConfiguration : IEntityTypeConfiguration<AiUsageCounter>
{
    public void Configure(EntityTypeBuilder<AiUsageCounter> builder)
    {
        builder.Property(c => c.FeatureKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => new { c.FeatureKey, c.UsageDate }).IsUnique();
    }
}
