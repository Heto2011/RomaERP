using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Audit;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.Property(l => l.UserId).HasMaxLength(450);
        builder.Property(l => l.UserName).HasMaxLength(256).IsRequired();
        builder.Property(l => l.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(l => l.Method).HasMaxLength(20).IsRequired();
        builder.HasIndex(l => l.OccurredAtUtc);
        builder.HasIndex(l => l.UserId);
    }
}
