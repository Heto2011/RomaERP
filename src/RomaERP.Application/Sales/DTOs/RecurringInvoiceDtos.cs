using RomaERP.Domain.Common;
using RomaERP.Domain.Sales;

namespace RomaERP.Application.Sales.DTOs;

public class RecurringLineInputDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
}

public class SaveRecurringInvoiceDto
{
    public string Title { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public int IntervalMonths { get; set; } = 1;
    public int DayOfMonth { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Credit;
    public string? Notes { get; set; }
    public List<RecurringLineInputDto> Lines { get; set; } = new();
}

public class IssueRecurringDto
{
    public DateTime InvoiceDate { get; set; }
    public Guid FiscalPeriodId { get; set; }
    /// <summary>Optional: edits for this one invoice (the template itself is not changed). Empty = the template's lines.</summary>
    public List<RecurringLineInputDto>? Lines { get; set; }
}

public class RecurringInvoiceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int IntervalMonths { get; set; }
    public int DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public decimal Total { get; set; }
    public int PendingCount { get; set; }
    public List<RecurringLineInputDto> Lines { get; set; } = new();
}

public class RecurringDueDto
{
    public Guid Id { get; set; }
    public Guid RecurringInvoiceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string PeriodKey { get; set; } = string.Empty;
    public RecurringDueStatus Status { get; set; }
    public PaymentTerm PaymentTerm { get; set; }
    public decimal Total { get; set; }
    public Guid? InvoiceId { get; set; }
    public List<RecurringLineInputDto> Lines { get; set; } = new();
}
