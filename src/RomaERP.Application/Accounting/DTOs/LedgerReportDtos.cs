using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class GeneralLedgerLineDto
{
    public DateTime EntryDate { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    /// <summary>Running balance after this line, in the account's natural direction (debit-nature accounts: debit minus credit).</summary>
    public decimal Balance { get; set; }
}

/// <summary>Every posted movement on one account in a period, with the balance carried in and the running balance —
/// the statement an accountant uses to trace a figure on the trial balance.</summary>
public class GeneralLedgerDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountNature Nature { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<GeneralLedgerLineDto> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}

public enum PartnerStatementLineType
{
    Invoice = 1,
    Payment = 2,
    CreditNote = 3,
    DebitNote = 4
}

public class PartnerStatementLineDto
{
    public DateTime Date { get; set; }
    public PartnerStatementLineType Type { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    /// <summary>What is still owed after this line: by the customer to us, or by us to the vendor.</summary>
    public decimal Balance { get; set; }
}

/// <summary>A customer's or vendor's account over a period, in the company's own currency: what was owed when the period
/// began, every invoice, payment and note, and what is owed at the end. For a customer it follows the Accounts
/// Receivable balance (credit and installment invoices, collections, credit/debit notes); for a vendor it follows
/// the Accounts Payable balance (credit purchase invoices and the payments made against them).</summary>
public class PartnerStatementDto
{
    public Guid PartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public bool IsCustomer { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<PartnerStatementLineDto> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}
