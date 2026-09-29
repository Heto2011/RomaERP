using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Notifications.DTOs;
using RomaERP.Application.Notifications.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppNotificationService _service;

    public WhatsAppController(IWhatsAppNotificationService service)
    {
        _service = service;
    }

    [HttpGet("status")]
    public async Task<ActionResult<WhatsAppStatusDto>> GetStatus(CancellationToken ct)
        => Ok(await _service.GetStatusAsync(ct));

    [HttpPost("credential")]
    public async Task<ActionResult<WhatsAppStatusDto>> SaveCredential(SaveWhatsAppCredentialDto dto, CancellationToken ct)
        => Ok(await _service.SaveCredentialAsync(dto, ct));

    [HttpPost("test")]
    public async Task<ActionResult<WhatsAppSendResultDto>> SendTest(CancellationToken ct)
        => Ok(await _service.SendTestMessageAsync(ct));

    [HttpPost("send-alerts")]
    public async Task<ActionResult<WhatsAppSendResultDto>> SendAlertsNow(CancellationToken ct)
        => Ok(await _service.SendAlertsDigestAsync(skipIfNothingSignificant: false, ct));
}
