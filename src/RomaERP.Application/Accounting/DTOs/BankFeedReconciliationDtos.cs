namespace RomaERP.Application.Accounting.DTOs;

public class BankFeedTransactionDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsMatched { get; set; }
    public Guid? MatchedJournalEntryLineId { get; set; }
}

public class UnmatchedGlLineDto
{
    public Guid JournalEntryLineId { get; set; }
    public Guid JournalEntryId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }

    /// <summary>Signed the same way as <see cref="BankFeedTransactionDto.Amount"/>: Debit − Credit.</summary>
    public decimal Amount { get; set; }
}

public class BankFeedReconciliationSummaryDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int MatchedCount { get; set; }
    public List<BankFeedTransactionDto> UnmatchedFeedLines { get; set; } = new();
    public List<UnmatchedGlLineDto> UnmatchedGlLines { get; set; } = new();

    /// <summary>Net movement of the imported/synced bank feed lines in the period.</summary>
    public decimal FeedNetMovement { get; set; }

    /// <summary>Net movement of the posted GL lines on this account in the period.</summary>
    public decimal GlNetMovement { get; set; }

    public bool IsBalanced => FeedNetMovement == GlNetMovement;
}

public class ImportBankFeedCsvResultDto
{
    public int ImportedCount { get; set; }
    public int AutoMatchedCount { get; set; }
}

public class SyncLiveBankFeedResultDto
{
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public int ImportedCount { get; set; }
    public int AutoMatchedCount { get; set; }
}

public class ManualMatchBankFeedDto
{
    public Guid BankFeedTransactionId { get; set; }
    public Guid JournalEntryLineId { get; set; }
}

public class BankFeedProviderStatusDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
}
