using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>A person who brings customers in for a commission. Customers sign up with the marketer's code (or link);
/// the marketer earns a share of every payment those customers make for a limited time.</summary>
public class Marketer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Unique, stored upper-case; what a customer types or what the referral link carries.</summary>
    public string Code { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Which marketer brought a company. One row per company, written once at signup.</summary>
public class MarketerReferral : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid MarketerId { get; set; }
    public DateTime ReferredAtUtc { get; set; } = DateTime.UtcNow;
}

public enum CommissionStatus
{
    /// <summary>Earned; waits out the hold period, then is payable.</summary>
    Accrued = 0,
    PaidOut = 1,
    /// <summary>Cancelled, e.g. the customer's payment was refunded.</summary>
    Voided = 2
}

/// <summary>The commission earned on one customer payment (one row per paid subscription invoice).</summary>
public class MarketerCommission : BaseEntity
{
    public Guid MarketerId { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime PaymentPaidAtUtc { get; set; }
    public decimal PaymentAmount { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>0.20 in the first year after the customer's first payment, 0.10 in the second.</summary>
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Payable from this date (payment date plus the hold period, so a refund can still cancel it).</summary>
    public DateTime EligibleAtUtc { get; set; }
    public CommissionStatus Status { get; set; } = CommissionStatus.Accrued;
    public DateTime? PaidOutAtUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>The commission rule, in one place: 20% of each payment during the first year after the customer's first payment,
/// 10% during the second year, nothing after that. Paid out after a 30-day hold.</summary>
public static class MarketerCommissionPolicy
{
    public const decimal FirstYearRate = 0.20m;
    public const decimal SecondYearRate = 0.10m;
    public const int HoldDays = 30;

    public static decimal RateFor(DateTime firstPaymentUtc, DateTime paymentUtc)
    {
        if (paymentUtc < firstPaymentUtc.AddYears(1)) return FirstYearRate;
        if (paymentUtc < firstPaymentUtc.AddYears(2)) return SecondYearRate;
        return 0m;
    }
}
