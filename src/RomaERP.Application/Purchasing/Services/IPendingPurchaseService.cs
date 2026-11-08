using RomaERP.Application.Purchasing.DTOs;

namespace RomaERP.Application.Purchasing.Services;

public interface IPendingPurchaseService
{
    Task<PendingPurchaseInvoiceDto> SubmitAsync(CreatePurchaseInvoiceDto dto, string userId, string userName, CancellationToken ct = default);
    /// <summary>Approvers see every request; everyone else only their own.</summary>
    Task<List<PendingPurchaseInvoiceDto>> ListAsync(string userId, bool canApprove, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> ApproveAsync(Guid id, string approverName, CancellationToken ct = default);
    Task RejectAsync(Guid id, string approverName, string? reason, CancellationToken ct = default);
}
