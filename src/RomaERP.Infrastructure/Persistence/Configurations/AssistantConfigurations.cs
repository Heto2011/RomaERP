using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.Assistant;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class ExpenseCaptureConfiguration : IEntityTypeConfiguration<ExpenseCapture>
{
    public void Configure(EntityTypeBuilder<ExpenseCapture> builder)
    {
        builder.Property(c => c.RawText).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.Currency).HasMaxLength(10);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.ProofFileName).HasMaxLength(255);
        builder.Property(c => c.ProofStoragePath).HasMaxLength(500);
        builder.Property(c => c.SubmittedByUserId).HasMaxLength(450).IsRequired();

        builder.HasOne(c => c.SuggestedAccount)
            .WithMany()
            .HasForeignKey(c => c.SuggestedAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.JournalEntry)
            .WithMany()
            .HasForeignKey(c => c.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.MatchedBankStatementLine)
            .WithMany()
            .HasForeignKey(c => c.MatchedBankStatementLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CustodyEmployee)
            .WithMany()
            .HasForeignKey(c => c.CustodyEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.ExpenseCapture)
            .HasForeignKey(m => m.ExpenseCaptureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class ExpensePolicyConfiguration : IEntityTypeConfiguration<ExpensePolicy>
{
    public void Configure(EntityTypeBuilder<ExpensePolicy> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.MaxAmount).HasPrecision(18, 2);
        builder.Property(p => p.RequireProofAbove).HasPrecision(18, 2);

        builder.HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class ExpenseCaptureMessageConfiguration : IEntityTypeConfiguration<ExpenseCaptureMessage>
{
    public void Configure(EntityTypeBuilder<ExpenseCaptureMessage> builder)
    {
        builder.Property(m => m.Content).HasMaxLength(2000).IsRequired();
    }
}

public class BankStatementImportConfiguration : IEntityTypeConfiguration<BankStatementImport>
{
    public void Configure(EntityTypeBuilder<BankStatementImport> builder)
    {
        builder.Property(i => i.FileName).HasMaxLength(255).IsRequired();
        builder.Property(i => i.ImportedByUserId).HasMaxLength(450).IsRequired();

        builder.HasOne(i => i.BankAccount)
            .WithMany()
            .HasForeignKey(i => i.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Lines)
            .WithOne(l => l.BankStatementImport)
            .HasForeignKey(l => l.BankStatementImportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(500);
        builder.Property(l => l.Amount).HasPrecision(18, 2);
    }
}

public class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.Property(r => r.Description).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Amount).HasPrecision(18, 2);
        builder.Property(r => r.Currency).HasMaxLength(10);

        builder.HasOne(r => r.Account)
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
