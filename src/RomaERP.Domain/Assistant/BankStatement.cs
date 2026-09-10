using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.Assistant;

public class BankStatementImport : AuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public Guid BankAccountId { get; set; }
    public Account? BankAccount { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string ImportedByUserId { get; set; } = string.Empty;

    public ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
}

public class BankStatementLine : BaseEntity
{
    public Guid BankStatementImportId { get; set; }
    public BankStatementImport? BankStatementImport { get; set; }

    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Positive = money out of the account (a withdrawal/expense), matching how card expenses are captured.</summary>
    public decimal Amount { get; set; }

    public bool IsMatched { get; set; }
}
