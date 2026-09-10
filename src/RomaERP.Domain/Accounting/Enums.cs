namespace RomaERP.Domain.Accounting;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum AccountNature
{
    Debit = 1,
    Credit = 2
}

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public enum DepreciationMethod
{
    StraightLine = 1,
    DecliningBalance = 2
}

public enum FixedAssetStatus
{
    Active = 1,
    Disposed = 2
}

public enum DepreciationRunStatus
{
    Draft = 1,
    Posted = 2
}
