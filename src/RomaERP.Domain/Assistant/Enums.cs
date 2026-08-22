namespace RomaERP.Domain.Assistant;

public enum PaymentMethod
{
    Unknown = 0,
    Cash = 1,
    Card = 2
}

public enum ExpenseCaptureStatus
{
    /// <summary>Initial parse is missing something (usually the amount); waiting on the user to clarify.</summary>
    AwaitingDetails = 1,

    /// <summary>Amount and category resolved; waiting for the user to say cash or card.</summary>
    AwaitingPaymentMethod = 2,

    /// <summary>Paid by card; waiting to be matched against an imported bank statement line.</summary>
    AwaitingReconciliation = 3,

    /// <summary>Ready to post (cash confirmed, or card matched to a bank line) but waiting on Admin approval.</summary>
    PendingApproval = 4,

    /// <summary>Approved and posted to the general ledger.</summary>
    Posted = 5,

    Rejected = 6
}

public enum ChatRole
{
    User = 1,
    Assistant = 2
}
