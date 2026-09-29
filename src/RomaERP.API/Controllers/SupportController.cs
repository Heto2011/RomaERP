using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Support.Services;
using RomaERP.Domain.Support;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.API.Controllers;

/// <summary>Customer-facing support ticket endpoints. Writes to the CENTRAL database directly — support is
/// one unified inbox across every tenant, unlike everything else here which is per-tenant isolated — so
/// this bypasses IApplicationDbContext/ApplicationDbContext entirely.</summary>
[ApiController]
[Authorize]
[Route("api/support")]
public class SupportController : ControllerBase
{
    private const long MaxAttachmentBytes = 5 * 1024 * 1024;

    private readonly CentralDbContext _central;
    private readonly ITenantContext _tenantContext;
    private readonly ISupportAiTriageService _triage;

    public SupportController(CentralDbContext central, ITenantContext tenantContext, ISupportAiTriageService triage)
    {
        _central = central;
        _tenantContext = tenantContext;
        _triage = triage;
    }

    [HttpGet("tickets")]
    public async Task<ActionResult<List<SupportTicketSummaryDto>>> GetMyTickets(CancellationToken ct)
    {
        var email = CurrentEmail();
        var tickets = await _central.SupportTickets
            .Where(t => t.TenantId == _tenantContext.TenantId && t.RequesterEmail == email)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(tickets.Select(ToSummaryDto).ToList());
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<ActionResult<SupportTicketDto>> GetTicket(Guid id, CancellationToken ct)
    {
        var ticket = await LoadOwnedTicketAsync(id, ct);
        return Ok(ToDetailDto(ticket));
    }

    [HttpPost("tickets")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<SupportTicketDto>> CreateTicket(
        [FromForm] CreateSupportTicketRequest request, [FromForm] List<IFormFile>? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            throw new ValidationAppException("لازم تكتب عنوان ووصف للمشكلة.");

        var attachmentEntities = await LoadAttachmentsAsync(attachments, ct);

        var ticket = new SupportTicket
        {
            TenantId = _tenantContext.TenantId,
            CompanyCode = _tenantContext.CompanyCode,
            RequesterEmail = CurrentEmail(),
            RequesterName = CurrentName(),
            Subject = request.Subject.Trim(),
            Status = SupportTicketStatus.Open,
        };
        ticket.Messages.Add(new SupportTicketMessage
        {
            SenderType = SupportMessageSender.Customer,
            SenderName = ticket.RequesterName,
            Body = request.Body.Trim(),
            Attachments = attachmentEntities,
        });

        _central.SupportTickets.Add(ticket);
        await _central.SaveChangesAsync(ct);

        // Best-effort AI first look — never blocks ticket creation on failure.
        var triageResult = await _triage.TriageAsync(ticket.Subject, request.Body, ct);
        if (triageResult.CanAnswer && triageResult.AnswerBody is not null)
        {
            ticket.Messages.Add(new SupportTicketMessage
            {
                TicketId = ticket.Id,
                SenderType = SupportMessageSender.Ai,
                SenderName = "المساعد الذكي",
                Body = triageResult.AnswerBody,
            });
            ticket.Status = SupportTicketStatus.AwaitingCustomer;
        }
        await _central.SaveChangesAsync(ct);

        return Ok(ToDetailDto(ticket));
    }

    [HttpPost("tickets/{id:guid}/messages")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<SupportTicketDto>> AddMessage(
        Guid id, [FromForm] AddSupportTicketMessageRequest request, [FromForm] List<IFormFile>? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ValidationAppException("لازم تكتب رد.");

        var ticket = await LoadOwnedTicketAsync(id, ct);
        if (ticket.Status == SupportTicketStatus.Closed)
            throw new ValidationAppException("التذكرة دي مقفولة، افتح تذكرة جديدة لو لسه محتاج مساعدة.");

        var attachmentEntities = await LoadAttachmentsAsync(attachments, ct);
        ticket.Messages.Add(new SupportTicketMessage
        {
            TicketId = ticket.Id,
            SenderType = SupportMessageSender.Customer,
            SenderName = ticket.RequesterName,
            Body = request.Body.Trim(),
            Attachments = attachmentEntities,
        });
        ticket.Status = SupportTicketStatus.Open;
        ticket.UpdatedAtUtc = DateTime.UtcNow;

        await _central.SaveChangesAsync(ct);
        return Ok(ToDetailDto(ticket));
    }

    [HttpGet("tickets/{ticketId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        var ticket = await LoadOwnedTicketAsync(ticketId, ct);
        var attachment = ticket.Messages.SelectMany(m => m.Attachments).FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException("SupportTicketAttachment", attachmentId);

        return File(attachment.Data, attachment.ContentType, attachment.FileName);
    }

    private async Task<SupportTicket> LoadOwnedTicketAsync(Guid id, CancellationToken ct)
    {
        var email = CurrentEmail();
        var ticket = await _central.SupportTickets
            .Include(t => t.Messages).ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket is null || ticket.TenantId != _tenantContext.TenantId || ticket.RequesterEmail != email)
            throw new NotFoundException("SupportTicket", id);

        return ticket;
    }

    private async Task<List<SupportTicketAttachment>> LoadAttachmentsAsync(List<IFormFile>? files, CancellationToken ct)
    {
        var result = new List<SupportTicketAttachment>();
        if (files is null) return result;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > MaxAttachmentBytes)
                throw new ValidationAppException($"الملف {file.FileName} أكبر من 5 ميجا، اختار ملف أصغر.");

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            result.Add(new SupportTicketAttachment
            {
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Data = stream.ToArray(),
            });
        }
        return result;
    }

    private string CurrentEmail() => User.FindFirstValue(ClaimTypes.Email)
        ?? throw new ValidationAppException("تعذر تحديد بريدك الإلكتروني من جلسة الدخول.");

    private string CurrentName() => User.FindFirstValue(ClaimTypes.Name) ?? CurrentEmail();

    private static SupportTicketSummaryDto ToSummaryDto(SupportTicket t) => new(
        t.Id, t.TicketNumber, t.CompanyCode, t.RequesterName, t.Subject, t.Status.ToString(), t.CreatedAtUtc, t.UpdatedAtUtc);

    private static SupportTicketDto ToDetailDto(SupportTicket t) => new(
        t.Id, t.TicketNumber, t.CompanyCode, t.RequesterEmail, t.RequesterName, t.Subject, t.Status.ToString(), t.CreatedAtUtc,
        t.Messages.OrderBy(m => m.CreatedAtUtc).Select(m => new SupportTicketMessageDto(
            m.Id, m.SenderType.ToString(), m.SenderName, m.Body, m.CreatedAtUtc,
            m.Attachments.Select(a => new SupportTicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.Data.LongLength)).ToList()
        )).ToList());
}
