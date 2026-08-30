using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Purchasing;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.Property(v => v.Code).HasMaxLength(20).IsRequired();
        builder.Property(v => v.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(v => v.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Phone).HasMaxLength(30);
        builder.Property(v => v.Email).HasMaxLength(150);
        builder.Property(v => v.TaxRegistrationNumber).HasMaxLength(50);
        builder.Property(v => v.ApBalance).HasPrecision(18, 2);
        builder.HasIndex(v => v.Code).IsUnique();

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.Property(i => i.InvoiceNumber).HasMaxLength(20).IsRequired();
        builder.Property(i => i.SubTotal).HasPrecision(18, 2);
        builder.Property(i => i.VatRate).HasPrecision(5, 4);
        builder.Property(i => i.VatAmount).HasPrecision(18, 2);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.PaidAmount).HasPrecision(18, 2);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();

        builder.HasOne(i => i.Vendor)
            .WithMany()
            .HasForeignKey(i => i.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.FiscalPeriod)
            .WithMany()
            .HasForeignKey(i => i.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.JournalEntry)
            .WithMany()
            .HasForeignKey(i => i.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Lines)
            .WithOne(l => l.PurchaseInvoice)
            .HasForeignKey(l => l.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne(p => p.PurchaseInvoice)
            .HasForeignKey(p => p.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class PurchaseInvoiceLineConfiguration : IEntityTypeConfiguration<PurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchasePaymentConfiguration : IEntityTypeConfiguration<PurchasePayment>
{
    public void Configure(EntityTypeBuilder<PurchasePayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Reference).HasMaxLength(200);

        builder.HasOne(p => p.JournalEntry)
            .WithMany()
            .HasForeignKey(p => p.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
