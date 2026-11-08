using RomaERP.Application.Sales.DTOs;

namespace RomaERP.Application.Sales.Services;

public interface ICustomerFollowUpService
{
    /// <summary>Customers with at least one invoice past its due date, worst first. The due date is the invoice date plus the
    /// customer's agreed credit days (for an installment invoice, the next installment not yet covered by payments).</summary>
    Task<List<OverdueCustomerDto>> GetOverdueAsync(DateTime? asOfDate = null, CancellationToken ct = default);
    Task<List<FollowUpDto>> GetFollowUpsAsync(Guid customerId, CancellationToken ct = default);
    Task<FollowUpDto> AddFollowUpAsync(CreateFollowUpDto dto, CancellationToken ct = default);
    /// <summary>Emails the customer a reminder (free, through the normal email provider) and records it as a follow-up.</summary>
    Task<ReminderEmailResultDto> SendReminderEmailAsync(Guid customerId, CancellationToken ct = default);
}
