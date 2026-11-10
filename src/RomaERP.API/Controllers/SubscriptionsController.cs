using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

public record SetPlanRequest(Guid PlanId);
public record SetBillingAccountRequest(Guid? BillingAccountId);
public record MarkInvoicePaidRequest(string? PaymentReference);
public record ActivatePaidRequest(Guid PlanId, string? PaymentReference, bool Annual = false);

public record SetBillingPeriodRequest(bool Annual);
public record GrantAiCreditsRequest(int Credits, string? Reason);

/// <summary>Platform-level billing console — spans every tenant, so (like SystemController) it's excluded
/// from TenantResolutionMiddleware and protected by a system key instead of a JWT/company code.</summary>
[ApiController]
[Route("api/system/subscriptions")]
[EnableRateLimiting("system-key")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionBillingService _billing;
    private readonly IConfiguration _configuration;
    private readonly RomaERP.Infrastructure.Persistence.Central.CentralDbContext _central;

    public SubscriptionsController(ISubscriptionBillingService billing, IConfiguration configuration, RomaERP.Infrastructure.Persistence.Central.CentralDbContext central)
    {
        _central = central;
        _billing = billing;
        _configuration = configuration;
    }

    [HttpGet("plans")]
    public async Task<ActionResult<List<SubscriptionPlanDto>>> GetPlans(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.GetPlansAsync(ct));
    }

    /// <summary>One page of companies; the total count is returned in the X-Total-Count header.</summary>
    [HttpGet("tenants")]
    public async Task<ActionResult<List<TenantSubscriptionDto>>> GetTenantSubscriptions(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var (items, total) = await _billing.GetTenantSubscriptionsPageAsync(search, page, pageSize, ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";
        return Ok(items);
    }

    /// <summary>The company's activity trail (password changes, plan/payment/status changes, user management...), newest first.</summary>
    [HttpGet("tenants/{tenantId:guid}/activity")]
    public async Task<ActionResult<List<TenantActivityDto>>> GetTenantActivity(Guid tenantId, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.GetTenantActivityAsync(tenantId, take, ct));
    }

    [HttpPut("tenants/{tenantId:guid}/plan")]
    public async Task<ActionResult<TenantSubscriptionDto>> SetPlan(Guid tenantId, SetPlanRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.SetPlanAsync(tenantId, request.PlanId, ct));
    }

    [HttpPut("tenants/{tenantId:guid}/billing-account")]
    public async Task<ActionResult<TenantSubscriptionDto>> SetBillingAccount(Guid tenantId, SetBillingAccountRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.SetBillingAccountAsync(tenantId, request.BillingAccountId, ct));
    }

    /// <summary>Adds prepaid AI credits to a company (a purchase made off-platform, or a gift). Negative numbers correct a mistake.</summary>
    [HttpPost("tenants/{tenantId:guid}/ai-credits")]
    public async Task<ActionResult> GrantAiCredits(Guid tenantId, GrantAiCreditsRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;
        if (request.Credits == 0 || Math.Abs(request.Credits) > 100000) return BadRequest(new { error = "عدد الرصيد غير صالح." });

        _central.TenantCreditEntries.Add(new RomaERP.Domain.Tenancy.TenantCreditEntry
        {
            TenantId = tenantId, Delta = request.Credits,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "System console grant" : request.Reason.Trim(),
            Actor = "system console"
        });
        await _central.SaveChangesAsync(ct);
        var balance = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SumAsync(
            System.Linq.Queryable.Select(System.Linq.Queryable.Where(_central.TenantCreditEntries, e => e.TenantId == tenantId), e => (int?)e.Delta), ct) ?? 0;
        return Ok(new { balance });
    }

    [HttpPost("tenants/{tenantId:guid}/suspend")]
    public async Task<ActionResult<TenantSubscriptionDto>> Suspend(Guid tenantId, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.SuspendAsync(tenantId, ct));
    }

    [HttpPost("tenants/{tenantId:guid}/activate-paid")]
    public async Task<ActionResult<TenantSubscriptionDto>> ActivatePaid(Guid tenantId, ActivatePaidRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.ActivatePaidAsync(tenantId, request.PlanId, request.PaymentReference,
            request.Annual ? RomaERP.Domain.Tenancy.BillingPeriod.Annual : RomaERP.Domain.Tenancy.BillingPeriod.Monthly, ct));
    }

    [HttpPut("tenants/{tenantId:guid}/billing-period")]
    public async Task<ActionResult<TenantSubscriptionDto>> SetBillingPeriod(Guid tenantId, SetBillingPeriodRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.SetBillingPeriodAsync(tenantId,
            request.Annual ? RomaERP.Domain.Tenancy.BillingPeriod.Annual : RomaERP.Domain.Tenancy.BillingPeriod.Monthly, ct));
    }

    [HttpPost("tenants/{tenantId:guid}/reactivate")]
    public async Task<ActionResult<TenantSubscriptionDto>> Reactivate(Guid tenantId, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.ReactivateAsync(tenantId, ct));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<SubscriptionInvoiceDto>>> GetInvoices([FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.GetInvoicesAsync(tenantId, ct));
    }

    [HttpPost("invoices/{invoiceId:guid}/mark-paid")]
    public async Task<ActionResult<SubscriptionInvoiceDto>> MarkInvoicePaid(Guid invoiceId, MarkInvoicePaidRequest request, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.MarkInvoicePaidAsync(invoiceId, request.PaymentReference, ct));
    }

    [HttpPost("run-billing-cycle")]
    public async Task<ActionResult<BillingRunResultDto>> RunBillingCycle(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.RunBillingCycleAsync(ct));
    }

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "إدارة الاشتراكات مش مفعّلة — لازم تضيف System:ProvisioningKey في الإعدادات." });

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
