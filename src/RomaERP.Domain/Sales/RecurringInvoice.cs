using RomaERP.Domain.Common;

namespace RomaERP.Domain.Sales;

public enum RecurringDueStatus { Pending = 1, Issued = 2, Skipped = 3 }

/// <summary>A fixed invoice a customer is billed again and again (a subscription, rent, a retainer). Nothing is issued by itself: when a period comes due the system prepares an entry that waits for a person to issue or skip it.</summary>
public class RecurringInvoice : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>1 = every month, 3 = every quarter, 6 = every half year, 12 = every year.</summary>
    public int IntervalMonths { get; set; } = 1;
    /// <summary>Day of the month it falls due; 29-31 mean the last day of a shorter month.</summary>
    public int DayOfMonth { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Credit;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RecurringInvoiceLine> Lines { get; set; } = new List<RecurringInvoiceLine>();
    public ICollection<RecurringInvoiceDue> Dues { get; set; } = new List<RecurringInvoiceDue>();
}

public class RecurringInvoiceLine : BaseEntity
{
    public Guid RecurringInvoiceId { get; set; }
    public RecurringInvoice? RecurringInvoice { get; set; }

    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
}

/// <summary>One period of a recurring invoice that has come due. It waits for a person (Pending) and then is Issued as a real invoice or Skipped.</summary>
public class RecurringInvoiceDue : AuditableEntity
{
    public Guid RecurringInvoiceId { get; set; }
    public RecurringInvoice? RecurringInvoice { get; set; }

    public DateTime DueDate { get; set; }
    /// <summary>yyyy-MM of the period: with the recurring invoice it is unique, so a period is prepared only once.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    public RecurringDueStatus Status { get; set; } = RecurringDueStatus.Pending;
    public Guid? InvoiceId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
}
