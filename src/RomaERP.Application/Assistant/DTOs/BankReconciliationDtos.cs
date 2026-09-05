namespace RomaERP.Application.Assistant.DTOs;

public class BankStatementLineDto
{
    public Guid Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsMatched { get; set; }
}

public class BankStatementImportDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int MatchedCount { get; set; }
}

public class ManualMatchDto
{
    public Guid ExpenseCaptureId { get; set; }
    public Guid BankStatementLineId { get; set; }
}
