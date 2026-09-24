using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.API.Contracts;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Support;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.API.Controllers;

/// <summary>Customer-facing view of the tenant's own subscription and invoices, plus the manual bank-transfer
/// flow: no payment gateway is live yet, so a customer pays by wiring the bank account below and clicking
/// "I've transferred", which opens a support ticket for a human to confirm and mark the invoice paid
/// (POST /api/system/subscriptions/invoices/{id}/mark-paid). Admin-only, like the rest of company billing.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/my-subscription")]
public class MySubscriptionController : ControllerBase
{
    private readonly ISubscriptionBillingService _billing;
    private readonly ITenantContext _tenantContext;
    private readonly CentralDbContext _central;
    private readonly IConfiguration _configuration;

    public MySubscriptionController(
        ISubscriptionBillingService billing, ITenantContext tenantContext, CentralDbContext central, IConfiguration configuration)
    {
        _billing = billing;
        _tenantContext = tenantContext;
        _central = central;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<TenantSubscriptionDto>> GetMySubscription(CancellationToken ct)
    {
        var all = await _billing.GetTenantSubscriptionsAsync(ct);
        var mine = all.FirstOrDefault(s => s.TenantId == _tenantContext.TenantId)
            ?? throw new NotFoundException("Subscription", _tenantContext.TenantId);
        return Ok(mine);
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<SubscriptionInvoiceDto>>> GetMyInvoices(CancellationToken ct)
        => Ok(await _billing.GetInvoicesAsync(_tenantContext.TenantId, ct));

    [HttpGet("bank-transfer")]
    public ActionResult<BankTransferInfoDto> GetBankTransferInfo()
    {
        var iban = _configuration["BankTransfer:Iban"];
        if (string.IsNullOrWhiteSpace(iban))
            return Ok(new BankTransferInfoDto(false, null, null, null, null));

        return Ok(new BankTransferInfoDto(
            true,
            _configuration["BankTransfer:AccountName"],
            iban,
            _configuration["BankTransfer:Swift"],
            _configuration["BankTransfer:BankName"]));
    }

    /// <summary>The customer declares they've wired an invoice's amount — this never marks the invoice paid
    /// by itself (no gateway confirms the transfer actually arrived), it just opens a support ticket with
    /// the details so a human can check the bank account and confirm.</summary>
    [HttpPost("invoices/{invoiceId:guid}/report-payment")]
    public async Task<IActionResult> ReportPayment(Guid invoiceId, ReportInvoicePaymentRequest request, CancellationToken ct)
    {
        var invoices = await _billing.GetInvoicesAsync(_tenantContext.TenantId, ct);
        var invoice = invoices.FirstOrDefault(i => i.Id == invoiceId)
            ?? throw new NotFoundException("SubscriptionInvoice", invoiceId);

        var email = CurrentEmail();
        var name = CurrentName();

        var bodyLines = new List<string>
        {
            $"إبلاغ عن تحويل دفعة اشتراك — فاتورة {invoice.PlanNameAr} بقيمة {invoice.TotalAmount} {invoice.Currency}.",
            $"الفترة: {invoice.PeriodStart:yyyy-MM-dd} إلى {invoice.PeriodEnd:yyyy-MM-dd}.",
        };
        if (!string.IsNullOrWhiteSpace(request.PaymentReference))
            bodyLines.Add($"مرجع التحويل: {request.PaymentReference}");
        if (!string.IsNullOrWhiteSpace(request.Note))
            bodyLines.Add($"ملاحظة العميل: {request.Note}");

        var ticket = new SupportTicket
        {
            TenantId = _tenantContext.TenantId,
            CompanyCode = _tenantContext.CompanyCode,
            RequesterEmail = email,
            RequesterName = name,
            Subject = "تأكيد تحويل دفعة اشتراك",
            Status = SupportTicketStatus.Open,
        };
        ticket.Messages.Add(new SupportTicketMessage
        {
            SenderType = SupportMessageSender.Customer,
            SenderName = name,
            Body = string.Join("\n", bodyLines),
        });

        _central.SupportTickets.Add(ticket);
        await _central.SaveChangesAsync(ct);

        return Ok(new { ticketId = ticket.Id, ticketNumber = ticket.TicketNumber });
    }

    private string CurrentEmail() => User.FindFirstValue(ClaimTypes.Email)
        ?? throw new ValidationAppException("تعذر تحديد بريدك الإلكتروني من جلسة الدخول.");

    private string CurrentName() => User.FindFirstValue(ClaimTypes.Name) ?? CurrentEmail();
}
