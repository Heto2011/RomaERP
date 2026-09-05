using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

public enum ManualProfitDimension
{
    Branch = 1,
    Channel = 2
}

/// <summary>A hand-entered revenue/cost figure for a branch or sales channel, for reports the system has no
/// transactional data to compute automatically (no Branch entity, no general sales-channel tracking outside
/// the restaurant module). The user types the numbers in and keeps them updated; RomaERP only computes the
/// profit/margin from what's entered — it never fabricates the Revenue/Cost themselves.</summary>
public class ManualProfitEntry : AuditableEntity
{
    public ManualProfitDimension Dimension { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime PeriodMonth { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
}
