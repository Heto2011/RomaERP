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

    /// <summary>Amount and category resolved; waiting for the user to say custody or company account.</summary>
    AwaitingFundingSource = 2,

    /// <summary>Funded from an employee custody advance; waiting to know which employee.</summary>
    AwaitingCustodyEmployee = 3,

    /// <summary>Funded directly from the company; waiting for the user to say cash or card.</summary>
    AwaitingPaymentMethod = 4,

    /// <summary>Paid by card; waiting to be matched against an imported bank statement line.</summary>
    AwaitingReconciliation = 5,

    /// <summary>Ready to post (cash confirmed, card matched to a bank line, or custody employee resolved) but waiting on Admin approval.</summary>
    PendingApproval = 6,

    /// <summary>Approved and posted to the general ledger.</summary>
    Posted = 7,

    Rejected = 8
}

public enum FundingSource
{
    Unknown = 0,

    /// <summary>Paid directly from the company's own cash or bank account.</summary>
    CompanyAccount = 1,

    /// <summary>Paid from money already advanced to an employee (عهدة) — settles against their custody balance, no cash/bank movement now.</summary>
    EmployeeCustody = 2
}

public enum ChatRole
{
    User = 1,
    Assistant = 2
}
