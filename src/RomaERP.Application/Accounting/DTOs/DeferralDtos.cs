using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

public class CreateDeferralDto
{
    public DeferralKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime StartMonth { get; set; }
    public int Months { get; set; }
    public Guid TargetAccountId { get; set; }
    public Guid SettlementAccountId { get; set; }
}

public class DeferralDto
{
    public Guid Id { get; set; }
    public DeferralKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime StartMonth { get; set; }
    public int Months { get; set; }
    public int RecognizedMonths { get; set; }
    public decimal RecognizedAmount { get; set; }
    public decimal Remaining { get; set; }
    public decimal MonthlyAmount { get; set; }
    public string TargetAccountName { get; set; } = string.Empty;
    public DeferralStatus Status { get; set; }
}

public class DeferralRunResultDto
{
    public int EntriesPosted { get; set; }
    public decimal AmountReleased { get; set; }
    public List<string> Skipped { get; set; } = new();
}
