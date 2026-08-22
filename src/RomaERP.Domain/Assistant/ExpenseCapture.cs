using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.Assistant;

/// <summary>
/// One expense captured through the natural-language chat assistant, tracked through its
/// lifecycle: parse -> ask cash/card -> (card) wait for bank match -> post to the GL.
/// </summary>
public class ExpenseCapture : AuditableEntity
{
    public string RawText { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public string? Description { get; set; }
    public DateTime EntryDate { get; set; }

    public Guid? SuggestedAccountId { get; set; }
    public Account? SuggestedAccount { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Unknown;
    public ExpenseCaptureStatus Status { get; set; } = ExpenseCaptureStatus.AwaitingPaymentMethod;

    public string? ProofFileName { get; set; }
    public string? ProofStoragePath { get; set; }

    public string SubmittedByUserId { get; set; } = string.Empty;

    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public Guid? MatchedBankStatementLineId { get; set; }
    public BankStatementLine? MatchedBankStatementLine { get; set; }

    public ICollection<ExpenseCaptureMessage> Messages { get; set; } = new List<ExpenseCaptureMessage>();
}

public class ExpenseCaptureMessage : BaseEntity
{
    public Guid ExpenseCaptureId { get; set; }
    public ExpenseCapture? ExpenseCapture { get; set; }

    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
