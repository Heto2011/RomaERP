using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RomaERP.Application.Common;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;

namespace RomaERP.API.Controllers;

public record CreatePortalLinkRequest(int? Days);

/// <summary>What the customer opens with their private link: no sign-in, the company comes from X-Company-Code and the customer from the secret in X-Portal-Token. Rate-limited.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/portal")]
public class CustomerPortalController : ControllerBase
{
    private readonly ICustomerPortalService _svc;
    public CustomerPortalController(ICustomerPortalService svc) { _svc = svc; }

    private string Token => Request.Headers.TryGetValue("X-Portal-Token", out var v) ? v.ToString() : string.Empty;

    [HttpGet("me")]
    [EnableRateLimiting("portal-view")]
    [ResponseCache(NoStore = true)]
    public async Task<ActionResult<CustomerPortalDto>> Me(CancellationToken ct) => Ok(await _svc.GetPortalAsync(Token, ct));

    [HttpGet("invoices/{id:guid}/pdf")]
    [EnableRateLimiting("portal-view")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct) => File(await _svc.GetInvoicePdfAsync(Token, id, ct), "application/pdf", $"invoice-{id}.pdf");
}

/// <summary>Making, checking and switching off a customer's link: the owner and the accountant.</summary>
[ApiController]
[Authorize(Policy = ModulePermissions.SalesPolicy)]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/sales/customers/{customerId:guid}/portal")]
public class CustomerPortalAdminController : ControllerBase
{
    private readonly ICustomerPortalService _svc;
    public CustomerPortalAdminController(ICustomerPortalService svc) { _svc = svc; }

    [HttpGet] public async Task<ActionResult<PortalLinkStatusDto>> Status(Guid customerId, CancellationToken ct) => Ok(await _svc.GetStatusAsync(customerId, ct));
    [HttpPost] public async Task<ActionResult<CreatedPortalLinkDto>> Create(Guid customerId, CreatePortalLinkRequest req, CancellationToken ct) => Ok(await _svc.CreateLinkAsync(customerId, req.Days, ct));
    [HttpDelete] public async Task<IActionResult> Revoke(Guid customerId, CancellationToken ct) { await _svc.RevokeAsync(customerId, ct); return NoContent(); }
}
