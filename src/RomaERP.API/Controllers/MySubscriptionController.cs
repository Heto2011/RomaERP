using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.API.Contracts;
using RomaERP.Application.Common;
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
    private readonly IHtmlToPdfRenderer _pdfRenderer;
    private readonly ILemonSqueezyService _lemon;
    private readonly IPlanLimitGuard _planLimits;

    public MySubscriptionController(
        ISubscriptionBillingService billing, ITenantContext tenantContext, CentralDbContext central, IConfiguration configuration,
        IHtmlToPdfRenderer pdfRenderer, ILemonSqueezyService lemon, IPlanLimitGuard planLimits)
    {
        _planLimits = planLimits;
        _lemon = lemon;
        _pdfRenderer = pdfRenderer;
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

    /// <summary>Which plans this company can pay for by card (outside Egypt only — Egypt pays through InstaPay).</summary>
    [HttpGet("card-payments")]
    public async Task<ActionResult<CardPaymentOptionsDto>> GetCardPaymentOptions(CancellationToken ct)
    {
        if (_tenantContext.Country == RomaERP.Domain.Tenancy.Country.Egypt || !_lemon.IsConfigured)
            return Ok(new CardPaymentOptionsDto(false, new List<CardPlanOptionDto>()));

        var plans = new List<CardPlanOptionDto>();
        foreach (var code in AllowedCardPlans())
        {
            var option = new CardPlanOptionDto(code, await _lemon.HasVariantAsync(code, false, IsUk(), ct), await _lemon.HasVariantAsync(code, true, IsUk(), ct));
            if (option.Monthly || option.Annual) plans.Add(option);
        }
        return Ok(new CardPaymentOptionsDto(plans.Count > 0, plans));
    }

    /// <summary>Starts a hosted card checkout for the chosen plan and returns its URL (the browser then redirects to it).</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<CardCheckoutDto>> StartCheckout(StartCheckoutRequest request, CancellationToken ct)
    {
        if (_tenantContext.Country == RomaERP.Domain.Tenancy.Country.Egypt)
            throw new ValidationAppException("الدفع في مصر بيتم عن طريق إنستاباي.");
        var planCode = (request.PlanCode ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedCardPlans().Contains(planCode) || !await _lemon.HasVariantAsync(planCode, request.Annual, IsUk(), ct))
            throw new ValidationAppException("الباقة دي مش متاحة للدفع بالبطاقة.");

        var baseUrl = (_configuration["App:PublicBaseUrl"] ?? "https://romagroup.app").TrimEnd('/');
        var returnPath = _tenantContext.ProductScope == RomaERP.Domain.Tenancy.ProductScope.PeopleOnly ? "/people/subscription" : "/my-subscription";
        var url = await _lemon.CreateCheckoutAsync(new LemonCheckoutRequest(
            _tenantContext.TenantId, _tenantContext.CompanyCode, CurrentEmail(), CurrentName(), planCode, request.Annual, baseUrl + returnPath + "?paid=1", IsUk()), ct);
        return Ok(new CardCheckoutDto(url));
    }

    // UK and Guernsey customers are priced in sterling on the website, so their card checkout uses their own USD price set.
    private bool IsUk() => _tenantContext.Country is RomaERP.Domain.Tenancy.Country.UnitedKingdom or RomaERP.Domain.Tenancy.Country.Guernsey;

    private string[] AllowedCardPlans()
        => _tenantContext.ProductScope == RomaERP.Domain.Tenancy.ProductScope.PeopleOnly
            ? new[] { "people" }
            : new[] { "essential", "business", "professional" };

    // Monthly USD price of one extra, per price set: Gulf/other vs Europe (UK, Guernsey). An extra Roma HR employee costs the same as an extra user.
    private decimal BranchPrice() => IsUk() ? 7m : 4m;
    private decimal UserPrice() => IsUk() ? 4m : 5m;

    private static readonly string[] PlanOrder = { "essential", "business", "professional" };

    /// <summary>What the company's plan includes, how many paid extras it has, and what it can still buy (card-paid companies only).</summary>
    [HttpGet("extras")]
    public async Task<ActionResult<ExtrasInfoDto>> GetExtras(CancellationToken ct)
    {
        var usage = await _planLimits.GetUsageAsync(ct);
        if (usage is null || !_lemon.IsConfigured)
            return Ok(new ExtrasInfoDto(false, false, "", "", 0, 0, 0, 0, 0, 0, 0, 0, false, false, new List<string>()));

        var upgrades = new List<string>();
        var from = Array.IndexOf(PlanOrder, usage.PlanCode.ToLowerInvariant());
        if (from >= 0)
            for (var i = from + 1; i < PlanOrder.Length; i++)
                if (await _lemon.HasVariantAsync(PlanOrder[i], false, IsUk(), ct)) upgrades.Add(PlanOrder[i]);

        return Ok(new ExtrasInfoDto(true, usage.IsHr, usage.PlanCode, usage.PlanName,
            usage.IncludedBranches, usage.PaidBranches, usage.UsedBranches,
            usage.IncludedUsers, usage.PaidUsers, usage.UsedUsers,
            BranchPrice(), UserPrice(),
            !usage.IsHr && await _lemon.HasVariantAsync("extra-branch", false, IsUk(), ct),
            await _lemon.HasVariantAsync("extra-user", false, IsUk(), ct),
            upgrades));
    }

    /// <summary>Adds or removes paid extras. The first extra returns a checkout URL; later changes are applied (and charged pro rata) immediately.</summary>
    [HttpPost("extras")]
    public async Task<ActionResult<ExtrasChangeResultDto>> ChangeExtras(ChangeExtrasRequest request, CancellationToken ct)
    {
        var usage = await _planLimits.GetUsageAsync(ct)
            ?? throw new ValidationAppException("الإضافات متاحة للشركات اللي بتدفع بالبطاقة بس.");
        var kind = string.Equals(request.Kind, "branch", StringComparison.OrdinalIgnoreCase) ? ExtraKind.Branch
            : string.Equals(request.Kind, "user", StringComparison.OrdinalIgnoreCase) ? ExtraKind.User
            : throw new ValidationAppException("نوع الإضافة غير معروف.");
        if (kind == ExtraKind.Branch && usage.IsHr) throw new ValidationAppException("Roma HR مفيهاش فروع.");
        if (request.Delta == 0 || Math.Abs(request.Delta) > 100) throw new ValidationAppException("العدد غير صحيح.");

        var used = kind == ExtraKind.Branch ? usage.UsedBranches - usage.IncludedBranches : usage.UsedUsers - usage.IncludedUsers;
        var baseUrl = (_configuration["App:PublicBaseUrl"] ?? "https://romagroup.app").TrimEnd('/');
        var returnPath = usage.IsHr ? "/people/subscription" : "/my-subscription";
        var result = await _lemon.ChangeExtrasAsync(_tenantContext.TenantId, kind, request.Delta, Math.Max(0, used),
            CurrentEmail(), CurrentName(), baseUrl + returnPath + "?extras=1", IsUk(), ct);
        return Ok(new ExtrasChangeResultDto(result.CheckoutUrl, result.NewQuantity));
    }

    /// <summary>Upgrades a card-paid company to a higher plan right away — no support ticket needed.</summary>
    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade(UpgradePlanRequest request, CancellationToken ct)
    {
        await _lemon.ChangePlanAsync(_tenantContext.TenantId, request.PlanCode ?? "", IsUk(), ct);
        return NoContent();
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<SubscriptionInvoiceDto>>> GetMyInvoices(CancellationToken ct)
        => Ok(await _billing.GetInvoicesAsync(_tenantContext.TenantId, ct));

    /// <summary>Egyptian customers pay through InstaPay only. Customers elsewhere pay by card through the payment
    /// gateway (Lemon Squeezy, not built yet), so no bank details are shown to them unless the owner switches on
    /// <c>Billing:ShowBankTransferAbroad</c> as a temporary fallback.</summary>
    /// <summary>The invoice as a branded PDF (Roma Group's own invoice to this customer).</summary>
    [HttpGet("invoices/{invoiceId:guid}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid invoiceId, [FromQuery] string? lang, CancellationToken ct)
    {
        var invoices = await _billing.GetInvoicesAsync(_tenantContext.TenantId, ct);
        var invoice = invoices.FirstOrDefault(i => i.Id == invoiceId)
            ?? throw new NotFoundException("SubscriptionInvoice", invoiceId);

        string? logo = null;
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "roma-logo.png");
        if (System.IO.File.Exists(logoPath))
            logo = Convert.ToBase64String(await System.IO.File.ReadAllBytesAsync(logoPath, ct));

        var html = SubscriptionInvoiceHtmlTemplate.Build(invoice, logo, arabic: !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase));
        var pdf = await _pdfRenderer.RenderAsync(html, ct);
        return File(pdf, "application/pdf", $"{SubscriptionInvoiceHtmlTemplate.NumberFor(invoice)}.pdf");
    }

    [HttpGet("bank-transfer")]
    public ActionResult<BankTransferInfoDto> GetBankTransferInfo()
    {
        var isEgypt = _tenantContext.Country == RomaERP.Domain.Tenancy.Country.Egypt;
        var iban = _configuration["BankTransfer:Iban"];
        var instaPayMobile = _configuration["InstaPay:Mobile"];
        var hasBankTransfer = !isEgypt && _configuration.GetValue<bool>("Billing:ShowBankTransferAbroad") && !string.IsNullOrWhiteSpace(iban);
        var hasInstaPay = isEgypt && !string.IsNullOrWhiteSpace(instaPayMobile);

        if (!hasBankTransfer && !hasInstaPay)
            return Ok(new BankTransferInfoDto(false, null, null, null, null, null));

        return Ok(new BankTransferInfoDto(
            true,
            hasBankTransfer ? _configuration["BankTransfer:AccountName"] : null,
            hasBankTransfer ? iban : null,
            hasBankTransfer ? _configuration["BankTransfer:Swift"] : null,
            hasBankTransfer ? _configuration["BankTransfer:BankName"] : null,
            hasInstaPay ? instaPayMobile : null));
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
