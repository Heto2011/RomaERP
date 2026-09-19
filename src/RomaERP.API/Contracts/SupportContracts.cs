namespace RomaERP.API.Contracts;

public record CreateSupportTicketRequest(string Subject, string Body);

public record AddSupportTicketMessageRequest(string Body);

public record SupportTicketAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);

public record SupportTicketMessageDto(
    Guid Id,
    string SenderType,
    string SenderName,
    string Body,
    DateTime CreatedAtUtc,
    List<SupportTicketAttachmentDto> Attachments);

public record SupportTicketDto(
    Guid Id,
    int TicketNumber,
    string CompanyCode,
    string RequesterEmail,
    string RequesterName,
    string Subject,
    string Status,
    DateTime CreatedAtUtc,
    List<SupportTicketMessageDto> Messages);

public record SupportTicketSummaryDto(
    Guid Id,
    int TicketNumber,
    string CompanyCode,
    string RequesterName,
    string Subject,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
