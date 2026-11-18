using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

public enum SubscriptionInvoiceStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Cancelled = 3,
}

/// <summary>One monthly billing invoice for one tenant, with the pricing breakdown frozen at generation
/// time (plan prices can change later without altering past invoices).</summary>
public class SubscriptionInvoice : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }

    public string PlanCode { get; set; } = string.Empty;
    public string PlanNameAr { get; set; } = string.Empty;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public decimal BaseAmount { get; set; }
    public int ExtraBranches { get; set; }
    public decimal ExtraBranchesAmount { get; set; }
    public int ExtraUsers { get; set; }
    public decimal ExtraUsersAmount { get; set; }
    public decimal MultiCompanyDiscountAmount { get; set; }
    /// <summary>The temporary plan discount (welcome offer or one the owner gave this company) taken off this invoice.</summary>
    public decimal PromoDiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "SAR";

    public SubscriptionInvoiceStatus Status { get; set; } = SubscriptionInvoiceStatus.Pending;
    public DateTime DueDateUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}
