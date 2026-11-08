using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public enum LoanStatus
{
    Active = 0,
    Paid = 1
}

/// <summary>Money the company borrowed (bank loan, loan from a partner), repaid in equal monthly installments.</summary>
public class Loan : AuditableEntity
{
    public string Description { get; set; } = string.Empty;
    public string? Lender { get; set; }
    public decimal Principal { get; set; }
    /// <summary>Yearly interest rate in percent (0 = interest free).</summary>
    public decimal AnnualInterestPercent { get; set; }
    public int TermMonths { get; set; }
    public DateTime DisbursementDate { get; set; }
    public DateTime FirstInstallmentDate { get; set; }

    /// <summary>The liability account the loan sits in (long-term loans by default).</summary>
    public Guid LoanAccountId { get; set; }
    public Account? LoanAccount { get; set; }
    /// <summary>Cash/bank the money came into and installments are paid from.</summary>
    public Guid BankAccountId { get; set; }
    public Account? BankAccount { get; set; }
    public Guid InterestExpenseAccountId { get; set; }
    public Account? InterestExpenseAccount { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;
    public ICollection<LoanInstallment> Installments { get; set; } = new List<LoanInstallment>();
}

public class LoanInstallment : AuditableEntity
{
    public Guid LoanId { get; set; }
    public Loan? Loan { get; set; }

    public int Number { get; set; }
    public DateTime DueDate { get; set; }
    public decimal PrincipalPart { get; set; }
    public decimal InterestPart { get; set; }
    public decimal Total { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
}
