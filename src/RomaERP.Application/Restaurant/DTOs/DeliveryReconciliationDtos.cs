namespace RomaERP.Application.Restaurant.DTOs;

public class DeliverySettlementLineDto
{
    public Guid Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class DeliverySettlementImportDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
}

/// <summary>ExpectedRevenue comes from real Delivery-channel RestaurantOrder revenue already recorded in
/// RomaERP (the same computation Sales Channel Profitability uses); ReceivedAmount sums whatever settlement
/// files have been uploaded for the period. There's no live API to any delivery platform, so this can't match
/// individual transactions the way bank reconciliation does — it's a period-level expected-vs-received check.</summary>
public class DeliveryReconciliationReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal Variance { get; set; }
}
