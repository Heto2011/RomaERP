using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

public record SetPlanRequest(Guid PlanId);
public record SetBillingAccountRequest(Guid? BillingAccountId);
public record MarkInvoicePaidRequest(string? PaymentReference);

/// <summary>Platform-level billing console — spans every tenant, so (like SystemController) it's excluded
/// from TenantResolutionMiddleware and protected by a system key instead of a JWT/company code.</summary>
[ApiController]
[Route("api/system/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionBillingService _billing;
    private readonly IConfiguration _configuration;

    public SubscriptionsController(ISubscriptionBillingService billing, IConfiguration configuration)
    {
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

    [HttpGet("tenants")]
    public async Task<ActionResult<List<TenantSubscriptionDto>>> GetTenantSubscriptions(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.GetTenantSubscriptionsAsync(ct));
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

    [HttpPost("tenants/{tenantId:guid}/suspend")]
    public async Task<ActionResult<TenantSubscriptionDto>> Suspend(Guid tenantId, CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        return Ok(await _billing.SuspendAsync(tenantId, ct));
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

        if (!Request.Headers.TryGetValue("X-System-Key", out var providedKey) || providedKey != systemKey)
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });

        return null;
    }
}
