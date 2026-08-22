using RomaERP.Application.Assistant.DTOs;

namespace RomaERP.Application.Assistant.Services;

public interface IExpenseAssistantService
{
    Task<ChatTurnResponseDto> SendMessageAsync(ChatTurnRequestDto request, string userId, CancellationToken ct = default);
    Task<List<ExpenseCaptureDto>> GetPendingReconciliationAsync(CancellationToken ct = default);
    Task<List<ExpenseCaptureDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<ExpenseCaptureDto> ApproveAsync(Guid captureId, CancellationToken ct = default);
    Task<ExpenseCaptureDto> RejectAsync(Guid captureId, CancellationToken ct = default);
    Task<ExpenseCaptureDto> AttachProofAsync(Guid captureId, string fileName, string storagePath, CancellationToken ct = default);
}
