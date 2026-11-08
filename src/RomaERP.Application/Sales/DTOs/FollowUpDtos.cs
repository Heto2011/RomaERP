namespace RomaERP.Application.Sales.DTOs;

public class OverdueInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal Outstanding { get; set; }
}

public class FollowUpDto
{
    public Guid Id { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime? PromisedPayDate { get; set; }
}

public class OverdueCustomerDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int PaymentTermsDays { get; set; }
    public decimal TotalOverdue { get; set; }
    public int OldestDaysOverdue { get; set; }
    public List<OverdueInvoiceDto> Invoices { get; set; } = new();
    public FollowUpDto? LastFollowUp { get; set; }
}

public class CreateFollowUpDto
{
    public Guid CustomerId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime? PromisedPayDate { get; set; }
}

public class ReminderEmailResultDto
{
    public bool Sent { get; set; }
    public string? Message { get; set; }
}
