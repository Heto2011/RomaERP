using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Support;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.API.Controllers;

/// <summary>Not tenant-scoped — used to create new tenants in the first place, so it's excluded from
/// TenantResolutionMiddleware and protected by a system key instead of a JWT/company code.</summary>
[ApiController]
[Route("api/system")]
[EnableRateLimiting("system-key")]
public class SystemController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioning;
    private readonly IUserTransferService _userTransfer;
    private readonly ISystemPasswordResetService _passwordReset;
    private readonly IConfiguration _configuration;
    private readonly CentralDbContext _central;

    public SystemController(
        ITenantProvisioningService provisioning,
        IUserTransferService userTransfer,
        ISystemPasswordResetService passwordReset,
        IConfiguration configuration,
        CentralDbContext central)
    {
        _provisioning = provisioning;
        _userTransfer = userTransfer;
        _passwordReset = passwordReset;
        _configuration = configuration;
        _central = central;
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<TenantDto>> CreateTenant(ProvisionTenantRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var tenant = await _provisioning.ProvisionAsync(request, ct);
        return Ok(tenant);
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<List<TenantDto>>> GetTenants([FromQuery] bool demoOnly, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _provisioning.GetTenantsAsync(demoOnly, ct));
    }

    [HttpPost("tenants/expire-demo")]
    public async Task<ActionResult<object>> ExpireDemoTenants(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var count = await _provisioning.DeactivateExpiredDemoTenantsAsync(ct);
        return Ok(new { deactivatedCount = count });
    }

    /// <summary>Internal-only tool for moving a user between companies. Each tenant's database is fully
    /// isolated, so this recreates the person's basic account (email, name, roles) in the target company
    /// and deactivates it in the source — it never carries over history (attendance, leave, payroll) that
    /// belongs to the old company, only the account itself.</summary>
    [HttpPost("users/transfer")]
    public async Task<ActionResult<TransferUserResult>> TransferUser(TransferUserRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _userTransfer.TransferAsync(request, ct));
    }

    /// <summary>Last resort when nobody can log in to a tenant (e.g. its only Admin is locked out) — resets
    /// a user's password directly, bypassing normal auth entirely, gated by the system key alone.</summary>
    [HttpPost("users/reset-password")]
    public async Task<IActionResult> ResetUserPassword(ResetSystemUserPasswordRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        await _passwordReset.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    /// <summary>One unified inbox across every tenant — this is the only way to see support tickets from
    /// more than one company at a time, since a normal tenant-scoped Admin JWT can only ever see its own.</summary>
    [HttpGet("support/tickets")]
    public async Task<ActionResult<List<SupportTicketSummaryDto>>> GetAllTickets([FromQuery] string? status, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var query = _central.SupportTickets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SupportTicketStatus>(status, true, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        var tickets = await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        return Ok(tickets.Select(t => new SupportTicketSummaryDto(
            t.Id, t.TicketNumber, t.CompanyCode, t.RequesterName, t.Subject, t.Status.ToString(), t.CreatedAtUtc, t.UpdatedAtUtc)).ToList());
    }

    [HttpGet("support/tickets/{id:guid}")]
    public async Task<ActionResult<SupportTicketDto>> GetTicket(Guid id, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var ticket = await _central.SupportTickets
            .Include(t => t.Messages).ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("SupportTicket", id);

        return Ok(ToDetailDto(ticket));
    }

    [HttpPost("support/tickets/{id:guid}/messages")]
    public async Task<ActionResult<SupportTicketDto>> ReplyToTicket(Guid id, AddSupportTicketMessageRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ValidationAppException("لازم تكتب رد.");

        var ticket = await _central.SupportTickets
            .Include(t => t.Messages).ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("SupportTicket", id);

        ticket.Messages.Add(new SupportTicketMessage
        {
            TicketId = ticket.Id,
            SenderType = SupportMessageSender.Support,
            SenderName = "فريق الدعم",
            Body = request.Body.Trim(),
        });
        ticket.Status = SupportTicketStatus.AwaitingCustomer;
        ticket.UpdatedAtUtc = DateTime.UtcNow;

        await _central.SaveChangesAsync(ct);
        return Ok(ToDetailDto(ticket));
    }

    [HttpPatch("support/tickets/{id:guid}/status")]
    public async Task<IActionResult> UpdateTicketStatus(Guid id, [FromBody] string status, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        if (!Enum.TryParse<SupportTicketStatus>(status, true, out var parsedStatus))
            throw new ValidationAppException("حالة غير معروفة.");

        var ticket = await _central.SupportTickets.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("SupportTicket", id);

        ticket.Status = parsedStatus;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await _central.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("support/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var attachment = await _central.SupportTicketAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new NotFoundException("SupportTicketAttachment", attachmentId);

        return File(attachment.Data, attachment.ContentType, attachment.FileName);
    }

    private static SupportTicketDto ToDetailDto(SupportTicket t) => new(
        t.Id, t.TicketNumber, t.CompanyCode, t.RequesterEmail, t.RequesterName, t.Subject, t.Status.ToString(), t.CreatedAtUtc,
        t.Messages.OrderBy(m => m.CreatedAtUtc).Select(m => new SupportTicketMessageDto(
            m.Id, m.SenderType.ToString(), m.SenderName, m.Body, m.CreatedAtUtc,
            m.Attachments.Select(a => new SupportTicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.Data.LongLength)).ToList()
        )).ToList());

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "إنشاء عملاء جدد مش مفعّل — لازم تضيف System:ProvisioningKey في الإعدادات." });

        if (!Request.Headers.TryGetValue("X-System-Key", out var providedKey) || !FixedTimeEquals(providedKey.ToString(), systemKey))
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });

        return null;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
