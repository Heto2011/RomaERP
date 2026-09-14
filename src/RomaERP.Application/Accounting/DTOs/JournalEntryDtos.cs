using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class JournalEntryLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid? CostCenterId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public class JournalEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public JournalEntryStatus Status { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class CreateJournalEntryLineDto
{
    public Guid AccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public class CreateJournalEntryDto
{
    public DateTime EntryDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public List<CreateJournalEntryLineDto> Lines { get; set; } = new();
}

public class TrialBalanceLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
}
