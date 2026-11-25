using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Sales;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(150);
        builder.Property(c => c.TaxRegistrationNumber).HasMaxLength(50);
        builder.Property(c => c.ArBalance).HasPrecision(18, 2);
        builder.Property(c => c.ContactPerson).HasMaxLength(150);
        builder.Property(c => c.Mobile).HasMaxLength(30);
        builder.Property(c => c.Address).HasMaxLength(300);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.CommercialRegisterNumber).HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(500);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2);
        builder.HasIndex(c => c.Code).IsUnique();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.Property(i => i.InvoiceNumber).HasMaxLength(20).IsRequired();
        builder.Property(i => i.CurrencyCode).HasMaxLength(10).IsRequired();
        builder.Property(i => i.ExchangeRateToFunctional).HasPrecision(18, 6);
        builder.Property(i => i.SubTotal).HasPrecision(18, 2);
        builder.Property(i => i.VatRate).HasPrecision(5, 4);
        builder.Property(i => i.VatAmount).HasPrecision(18, 2);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.PaidAmount).HasPrecision(18, 2);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.EInvoiceExternalUuid).HasMaxLength(100);
        builder.Property(i => i.EInvoiceHash).HasMaxLength(200);
        builder.Property(i => i.EInvoiceErrorMessage).HasMaxLength(1000);
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();

        builder.HasOne(i => i.Customer)
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.FiscalPeriod)
            .WithMany()
            .HasForeignKey(i => i.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.JournalEntry)
            .WithMany()
            .HasForeignKey(i => i.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Warehouse)
            .WithMany()
            .HasForeignKey(i => i.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Lines)
            .WithOne(l => l.SalesInvoice)
            .HasForeignKey(l => l.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne(p => p.SalesInvoice)
            .HasForeignKey(p => p.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesInstallmentLineConfiguration : IEntityTypeConfiguration<SalesInstallmentLine>
{
    public void Configure(EntityTypeBuilder<SalesInstallmentLine> builder)
    {
        builder.Property(l => l.Amount).HasPrecision(18, 2);

        builder.HasOne(l => l.SalesInvoice)
            .WithMany(i => i.InstallmentLines)
            .HasForeignKey(l => l.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesPaymentConfiguration : IEntityTypeConfiguration<SalesPayment>
{
    public void Configure(EntityTypeBuilder<SalesPayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.ExchangeRateToFunctional).HasPrecision(18, 6);
        builder.Property(p => p.Reference).HasMaxLength(200);

        builder.HasOne(p => p.JournalEntry)
            .WithMany()
            .HasForeignKey(p => p.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class SalesNoteConfiguration : IEntityTypeConfiguration<SalesNote>
{
    public void Configure(EntityTypeBuilder<SalesNote> builder)
    {
        builder.Property(n => n.NoteNumber).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Reason).HasMaxLength(500).IsRequired();
        builder.Property(n => n.SubTotal).HasPrecision(18, 2);
        builder.Property(n => n.VatRate).HasPrecision(5, 4);
        builder.Property(n => n.VatAmount).HasPrecision(18, 2);
        builder.Property(n => n.TotalAmount).HasPrecision(18, 2);
        builder.Property(n => n.Notes).HasMaxLength(1000);
        builder.Property(n => n.EInvoiceExternalUuid).HasMaxLength(100);
        builder.Property(n => n.EInvoiceHash).HasMaxLength(200);
        builder.Property(n => n.EInvoiceErrorMessage).HasMaxLength(1000);
        builder.HasIndex(n => n.NoteNumber).IsUnique();

        builder.HasOne(n => n.OriginalInvoice)
            .WithMany()
            .HasForeignKey(n => n.OriginalInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Customer)
            .WithMany()
            .HasForeignKey(n => n.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.FiscalPeriod)
            .WithMany()
            .HasForeignKey(n => n.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.JournalEntry)
            .WithMany()
            .HasForeignKey(n => n.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(n => n.Lines)
            .WithOne(l => l.SalesNote)
            .HasForeignKey(l => l.SalesNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}

public class SalesNoteLineConfiguration : IEntityTypeConfiguration<SalesNoteLine>
{
    public void Configure(EntityTypeBuilder<SalesNoteLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
    }
}

public class CustomerFollowUpConfiguration : IEntityTypeConfiguration<CustomerFollowUp>
{
    public void Configure(EntityTypeBuilder<CustomerFollowUp> builder)
    {
        builder.Property(f => f.Method).HasMaxLength(40).IsRequired();
        builder.Property(f => f.Note).HasMaxLength(500);
        builder.HasIndex(f => new { f.CustomerId, f.FollowUpDate });
        builder.HasOne(f => f.Customer).WithMany().HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}

public class SalesQuotationConfiguration : IEntityTypeConfiguration<SalesQuotation>
{
    public void Configure(EntityTypeBuilder<SalesQuotation> builder)
    {
        builder.Property(q => q.QuotationNumber).HasMaxLength(20).IsRequired();
        builder.Property(q => q.Notes).HasMaxLength(1000);
        builder.Property(q => q.Terms).HasMaxLength(2000);
        builder.Property(q => q.SubTotal).HasPrecision(18, 2);
        builder.Property(q => q.VatAmount).HasPrecision(18, 2);
        builder.Property(q => q.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(q => q.QuotationNumber).IsUnique();
        builder.HasOne(q => q.Customer).WithMany().HasForeignKey(q => q.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(q => q.Lines).WithOne(l => l.SalesQuotation).HasForeignKey(l => l.SalesQuotationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesQuotationLineConfiguration : IEntityTypeConfiguration<SalesQuotationLine>
{
    public void Configure(EntityTypeBuilder<SalesQuotationLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.VatRate).HasPrecision(5, 4);
        builder.Property(l => l.VatAmount).HasPrecision(18, 2);
        builder.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<RomaERP.Domain.Purchasing.PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<RomaERP.Domain.Purchasing.PurchaseOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.ApprovedBy).HasMaxLength(100);
        builder.Property(o => o.SubTotal).HasPrecision(18, 2);
        builder.Property(o => o.VatAmount).HasPrecision(18, 2);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasOne(o => o.Vendor).WithMany().HasForeignKey(o => o.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne(l => l.PurchaseOrder).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<RomaERP.Domain.Purchasing.PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<RomaERP.Domain.Purchasing.PurchaseOrderLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.VatRate).HasPrecision(5, 4);
        builder.Property(l => l.VatAmount).HasPrecision(18, 2);
        builder.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseNoteConfiguration : IEntityTypeConfiguration<RomaERP.Domain.Purchasing.PurchaseNote>
{
    public void Configure(EntityTypeBuilder<RomaERP.Domain.Purchasing.PurchaseNote> builder)
    {
        builder.Property(n => n.NoteNumber).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Reason).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Notes).HasMaxLength(1000);
        builder.Property(n => n.SubTotal).HasPrecision(18, 2);
        builder.Property(n => n.VatAmount).HasPrecision(18, 2);
        builder.Property(n => n.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(n => n.NoteNumber).IsUnique();
        builder.HasOne(n => n.OriginalInvoice).WithMany().HasForeignKey(n => n.OriginalInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.Vendor).WithMany().HasForeignKey(n => n.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.FiscalPeriod).WithMany().HasForeignKey(n => n.FiscalPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.JournalEntry).WithMany().HasForeignKey(n => n.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(n => n.Lines).WithOne(l => l.PurchaseNote).HasForeignKey(l => l.PurchaseNoteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseNoteLineConfiguration : IEntityTypeConfiguration<RomaERP.Domain.Purchasing.PurchaseNoteLine>
{
    public void Configure(EntityTypeBuilder<RomaERP.Domain.Purchasing.PurchaseNoteLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.VatRate).HasPrecision(5, 4);
        builder.Property(l => l.VatAmount).HasPrecision(18, 2);
        builder.HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecurringInvoiceConfiguration : IEntityTypeConfiguration<RecurringInvoice>
{
    public void Configure(EntityTypeBuilder<RecurringInvoice> builder)
    {
        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Lines).WithOne(l => l.RecurringInvoice).HasForeignKey(l => l.RecurringInvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Dues).WithOne(d => d.RecurringInvoice).HasForeignKey(d => d.RecurringInvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecurringInvoiceLineConfiguration : IEntityTypeConfiguration<RecurringInvoiceLine>
{
    public void Configure(EntityTypeBuilder<RecurringInvoiceLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.VatRate).HasPrecision(5, 4);
    }
}

public class RecurringInvoiceDueConfiguration : IEntityTypeConfiguration<RecurringInvoiceDue>
{
    public void Configure(EntityTypeBuilder<RecurringInvoiceDue> builder)
    {
        builder.Property(d => d.PeriodKey).HasMaxLength(7).IsRequired();
        builder.Property(d => d.DecidedBy).HasMaxLength(100);
        builder.HasIndex(d => new { d.RecurringInvoiceId, d.PeriodKey }).IsUnique();
    }
}

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.DeliveryAddress).HasMaxLength(300);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.DeliveredBy).HasMaxLength(100);
        builder.Property(o => o.SubTotal).HasPrecision(18, 2);
        builder.Property(o => o.VatAmount).HasPrecision(18, 2);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne(l => l.SalesOrder).HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.VatRate).HasPrecision(5, 4);
        builder.Property(l => l.VatAmount).HasPrecision(18, 2);
        builder.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
