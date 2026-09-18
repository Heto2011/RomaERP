using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

/// <summary>One line from a bank account's real-world activity, brought in either by uploading a
/// statement (<see cref="Source"/> = "Manual") or, once a live provider is connected, automatically
/// (<see cref="Source"/> = "Live") — mirrors how <see cref="ExchangeRate.Source"/> distinguishes a typed
/// rate from a fetched one. Matched against a posted <see cref="JournalEntryLine"/> on the same GL
/// account to confirm the books agree with the bank.</summary>
public class BankFeedTransaction : AuditableEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Positive = money into the account (deposit), negative = money out (withdrawal) — signed
    /// the same way a GL asset account moves (Debit increases, Credit decreases).</summary>
    public decimal Amount { get; set; }

    public string Source { get; set; } = "Manual";

    /// <summary>The bank/provider's own id for this transaction, used to avoid re-importing the same line
    /// on a later live sync. Null for a manually-uploaded (CSV) line.</summary>
    public string? ExternalId { get; set; }

    public bool IsMatched { get; set; }

    public Guid? MatchedJournalEntryLineId { get; set; }
    public JournalEntryLine? MatchedJournalEntryLine { get; set; }
}
