using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.EInvoicing;
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

        builder.Property(c => c.EInvoicingClientId).HasMaxLength(200);
        builder.Property(c => c.EInvoicingLastInvoiceHash).HasMaxLength(200);

        builder.Property(c => c.EInvoicingZatcaOrganizationIdentifier).HasMaxLength(15);
        builder.Property(c => c.EInvoicingZatcaSolutionName).HasMaxLength(100);
        builder.Property(c => c.EInvoicingZatcaModel).HasMaxLength(100);
        builder.Property(c => c.EInvoicingZatcaDeviceSerialNumber).HasMaxLength(100);
        builder.Property(c => c.EInvoicingZatcaOrganizationUnitName).HasMaxLength(200);
        builder.Property(c => c.EInvoicingZatcaAddress).HasMaxLength(200);
        builder.Property(c => c.EInvoicingZatcaBusinessCategory).HasMaxLength(200);
        builder.Property(c => c.EInvoicingZatcaInvoiceType).HasMaxLength(4).IsRequired();
        builder.Property(c => c.EInvoicingZatcaComplianceRequestId).HasMaxLength(100);
        // EF's migration scaffolding doesn't read C# property initializers for enum defaults — without this,
        // the generated column default is the CLR default (0), which isn't a valid ZatcaOnboardingStage member
        // (NotStarted = 1, chosen deliberately so 0 stays recognizable as "never explicitly set").
        builder.Property(c => c.EInvoicingZatcaOnboardingStage).HasDefaultValue(ZatcaOnboardingStage.NotStarted);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
