using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.Assistant;

/// <summary>A fixed monthly cost (rent, electricity, subscriptions...) the company marked as recurring. Each month, on its day,
/// the system prepares an expense for it and puts it in front of the approver — it never posts anything by itself.</summary>
public class RecurringExpense : AuditableEntity
{
    public string Description { get; set; } = string.Empty;

    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>The usual amount. Null = the amount changes every month and is typed in when the expense is approved.</summary>
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "EGP";

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>Day of the month the cost falls due (1-28, so it exists in every month).</summary>
    public int DayOfMonth { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>First day of the month the last expense was prepared for — keeps a month from being prepared twice.</summary>
    public DateTime? LastPreparedMonth { get; set; }
}
