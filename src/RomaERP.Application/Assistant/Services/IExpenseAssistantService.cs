using RomaERP.Application.Assistant.DTOs;

namespace RomaERP.Application.Assistant.Services;

public interface IExpenseAssistantService
{
    Task<ChatTurnResponseDto> SendMessageAsync(ChatTurnRequestDto request, string userId, CancellationToken ct = default);

    /// <summary>Starts a brand-new expense capture from a photo of a receipt/invoice instead of typed text —
    /// runs OCR+extraction (Claude vision) and, when the amount is legible, jumps straight to the
    /// funding-source question exactly as if the user had typed it in.</summary>
    Task<ChatTurnResponseDto> StartFromReceiptImageAsync(byte[] imageBytes, string mediaType, string userId, CancellationToken ct = default);
    Task<List<ExpenseCaptureDto>> GetPendingReconciliationAsync(CancellationToken ct = default);
    Task<List<ExpenseCaptureDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<ExpenseCaptureDto> ApproveAsync(Guid captureId, CancellationToken ct = default);
    Task<ExpenseCaptureDto> RejectAsync(Guid captureId, CancellationToken ct = default);
    Task<ExpenseCaptureDto> AttachProofAsync(Guid captureId, string fileName, string storagePath, CancellationToken ct = default);
}
