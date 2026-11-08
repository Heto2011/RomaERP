using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public enum DeferralKind
{
    /// <summary>Paid in advance (a year's rent, an annual insurance): sits as an asset and is charged to expense month by month.</summary>
    PrepaidExpense = 0,
    /// <summary>Collected in advance (an annual subscription): sits as a liability and is earned as revenue month by month.</summary>
    DeferredRevenue = 1
}

public enum DeferralStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2
}

/// <summary>A prepaid expense or deferred revenue spread evenly over a number of months. Creating it posts the cash side
/// (to Prepaid Expenses or Unearned Revenue); each month it is released into the expense or revenue account.</summary>
public class DeferralSchedule : AuditableEntity
{
    public DeferralKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    /// <summary>First day of the first month that bears the expense / earns the revenue.</summary>
    public DateTime StartMonth { get; set; }
    public int Months { get; set; }

    public int RecognizedMonths { get; set; }
    public decimal RecognizedAmount { get; set; }

    /// <summary>The expense account (prepaid) or revenue account (deferred) the monthly amount is released into.</summary>
    public Guid TargetAccountId { get; set; }
    public Account? TargetAccount { get; set; }

    /// <summary>Where the money moved at the start: cash/bank (or a payable / receivable).</summary>
    public Guid SettlementAccountId { get; set; }
    public Account? SettlementAccount { get; set; }

    public Guid? SourceJournalEntryId { get; set; }
    public DeferralStatus Status { get; set; } = DeferralStatus.Active;
}
