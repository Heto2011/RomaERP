namespace RomaERP.Application.Accounting.DTOs;

public class BalanceTransferRequestDto
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    /// <summary>The balance is taken as of this date, and the entry is dated on it.</summary>
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
}

public class BalanceTransferPreviewDto
{
    public string FromCode { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ToCode { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    /// <summary>Debit minus credit of the old account up to the date. Positive = a debit balance.</summary>
    public decimal Balance { get; set; }
    public decimal Amount { get; set; }
    public string DebitAccount { get; set; } = string.Empty;
    public string CreditAccount { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool CanApply { get; set; }
    public string? Blocked { get; set; }
}

public class BalanceTransferResultDto
{
    public string EntryNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
