using RomaERP.Domain.Assistant;

namespace RomaERP.Application.Assistant.DTOs;

public class ChatTurnRequestDto
{
    public Guid? CaptureId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ChatMessageDto
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class ChatTurnResponseDto
{
    public Guid CaptureId { get; set; }
    public ExpenseCaptureStatus Status { get; set; }
    public string AssistantReply { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = new();
    public ExpenseCaptureDto? Capture { get; set; }
}

public class ExpenseCaptureDto
{
    public Guid Id { get; set; }
    public string RawText { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EntryDate { get; set; }
    public Guid? SuggestedAccountId { get; set; }
    public string? SuggestedAccountCode { get; set; }
    public string? SuggestedAccountName { get; set; }
    public FundingSource FundingSource { get; set; }
    public Guid? CustodyEmployeeId { get; set; }
    public string? CustodyEmployeeName { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public ExpenseCaptureStatus Status { get; set; }
    public string? ProofFileName { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string SubmittedByUserId { get; set; } = string.Empty;
}
