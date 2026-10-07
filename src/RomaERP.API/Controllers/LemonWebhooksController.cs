using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.API.Controllers;

/// <summary>Receives Lemon Squeezy's webhooks. Outside tenant resolution and JWT auth on purpose (Lemon is the caller):
/// the only proof of origin is the HMAC signature, so anything unsigned or wrongly signed is rejected untouched.</summary>
[ApiController]
[Route("api/webhooks/lemonsqueezy")]
public class LemonWebhooksController : ControllerBase
{
    private readonly ILemonSqueezyService _lemon;
    private readonly ILogger<LemonWebhooksController> _logger;

    public LemonWebhooksController(ILemonSqueezyService lemon, ILogger<LemonWebhooksController> logger)
    {
        _lemon = lemon;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        if (!_lemon.VerifySignature(body, Request.Headers["X-Signature"].ToString()))
            return Unauthorized();

        try
        {
            await _lemon.HandleWebhookAsync(body, ct);
        }
        catch (Exception ex)
        {
            // A 500 makes Lemon retry later, which is what we want for a transient failure; the handler is idempotent.
            _logger.LogError(ex, "Lemon Squeezy webhook handling failed.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        return Ok();
    }
}
