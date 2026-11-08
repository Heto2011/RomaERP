using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class CreateLoanDto
{
    public string Description { get; set; } = string.Empty;
    public string? Lender { get; set; }
    public decimal Principal { get; set; }
    public decimal AnnualInterestPercent { get; set; }
    public int TermMonths { get; set; }
    public DateTime DisbursementDate { get; set; }
    public DateTime FirstInstallmentDate { get; set; }
    public Guid LoanAccountId { get; set; }
    public Guid BankAccountId { get; set; }
    /// <summary>Optional: when empty the standard "loan interest expense" account is used (and created if missing).</summary>
    public Guid? InterestExpenseAccountId { get; set; }
}

public class LoanInstallmentDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public DateTime DueDate { get; set; }
    public decimal PrincipalPart { get; set; }
    public decimal InterestPart { get; set; }
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
}

public class LoanDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Lender { get; set; }
    public decimal Principal { get; set; }
    public decimal AnnualInterestPercent { get; set; }
    public int TermMonths { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal Remaining { get; set; }
    public int PaidInstallments { get; set; }
    public DateTime? NextDueDate { get; set; }
    public decimal? NextDueAmount { get; set; }
    public bool NextIsOverdue { get; set; }
    public LoanStatus Status { get; set; }
    public List<LoanInstallmentDto> Installments { get; set; } = new();
}

public class PayInstallmentDto
{
    public DateTime PaymentDate { get; set; }
}
