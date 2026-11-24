using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Application.Support.Services;

public interface ISupportNotifier
{
    /// <summary>Tells the owner a customer opened a ticket or wrote again, so nothing waits unseen.</summary>
    Task NotifyOwnerAsync(int ticketNumber, string companyCode, string requesterName, string subject, string message, bool isNew, bool aiAnswered, CancellationToken ct = default);
}

