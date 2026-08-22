using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.Property(c => c.CompanyNameAr).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CompanyNameEn).HasMaxLength(200).IsRequired();
        builder.Property(c => c.TaxRegistrationNumber).HasMaxLength(50);
        builder.Property(c => c.VatRate).HasPrecision(5, 4);
        builder.Property(c => c.DefaultCurrency).HasMaxLength(10).IsRequired();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
