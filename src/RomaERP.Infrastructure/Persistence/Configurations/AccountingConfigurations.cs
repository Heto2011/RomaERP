using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Accounting;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.Property(a => a.Code).HasMaxLength(20).IsRequired();
        builder.Property(a => a.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(a => a.NameEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();

        builder.HasOne(a => a.ParentAccount)
            .WithMany(a => a.Children)
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(50).IsRequired();
        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}

public class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(50).IsRequired();

        builder.HasOne(p => p.FiscalYear)
            .WithMany(y => y.Periods)
            .HasForeignKey(p => p.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.Property(e => e.EntryNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.HasIndex(e => e.EntryNumber).IsUnique();

        builder.HasOne(e => e.FiscalPeriod)
            .WithMany()
            .HasForeignKey(e => e.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Lines)
            .WithOne(l => l.JournalEntry)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.TotalDebit);
        builder.Ignore(e => e.TotalCredit);
        builder.Ignore(e => e.IsBalanced);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.Property(l => l.Debit).HasPrecision(18, 2);
        builder.Property(l => l.Credit).HasPrecision(18, 2);
        builder.Property(l => l.Description).HasMaxLength(500);

        builder.HasOne(l => l.Account)
            .WithMany(a => a.JournalEntryLines)
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.CostCenter)
            .WithMany()
            .HasForeignKey(l => l.CostCenterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
