using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

/// <summary>Builds and stages a simple two-line posted journal entry (used by system-generated postings).</summary>
public static class SimpleJournalEntryFactory
{
    public static async Task<JournalEntry> CreatePostedAsync(
        IApplicationDbContext context,
        DateTime entryDate,
        Guid fiscalPeriodId,
        string description,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        string? reference = null,
        CancellationToken ct = default)
    {
        var entryNumber = $"JV-{(await context.JournalEntries.CountAsync(ct) + 1):D6}";

        var entry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = entryDate,
            FiscalPeriodId = fiscalPeriodId,
            Description = description,
            Reference = reference,
            Status = JournalEntryStatus.Posted,
            Lines =
            {
                new JournalEntryLine { LineNumber = 1, AccountId = debitAccountId, Debit = amount, Credit = 0, Description = description },
                new JournalEntryLine { LineNumber = 2, AccountId = creditAccountId, Debit = 0, Credit = amount, Description = description }
            }
        };

        context.JournalEntries.Add(entry);
        return entry;
    }
}
