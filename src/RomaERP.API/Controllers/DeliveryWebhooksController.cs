using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

/// <summary>Public inbound endpoint delivery platforms call directly — never called by our own frontend.
/// Not [Authorize]d like the rest of the API (the caller is HungerStation/Jahez/Mrsool's servers, not a
/// logged-in user); authenticity instead comes from IDeliveryPlatformProvider.VerifySignature inside
/// DeliveryOrderIntakeService. Always returns 200 (even on a business failure like an unmapped item) so
/// the platform doesn't retry-storm — the real outcome is recorded as a DeliveryWebhookEvent for staff to
/// see and retry, not on this response.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/delivery-webhooks")]
public class DeliveryWebhooksController : ControllerBase
{
    private readonly IDeliveryOrderIntakeService _intakeService;

    public DeliveryWebhooksController(IDeliveryOrderIntakeService intakeService)
    {
        _intakeService = intakeService;
    }

    [HttpPost("{platform}")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<DeliveryWebhookEventDto>> Receive(string platform, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var signatureHeader = Request.Headers.TryGetValue("X-Webhook-Signature", out var values) ? values.ToString() : null;

        return Ok(await _intakeService.ReceiveWebhookAsync(platform, rawBody, signatureHeader, ct));
    }
}
