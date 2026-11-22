using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

public record SetLocationBody(double Latitude, double Longitude);

/// <summary>Staff side of ordering from the phone: the requests waiting for approval, and the settings/codes for the QR tables.</summary>
[ApiController]
[Authorize(Policy = ModulePermissions.POSPolicy)]
[Route("api/qr-orders")]
public class QrOrdersController : ControllerBase
{
    private readonly IQrOrderingService _qr;
    public QrOrdersController(IQrOrderingService qr) => _qr = qr;

    [HttpGet("pending")]
    public async Task<ActionResult<List<QrOrderRequestDto>>> Pending(CancellationToken ct) => Ok(await _qr.GetPendingAsync(ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<object>> Approve(Guid id, CancellationToken ct) => Ok(new { orderId = await _qr.ApproveAsync(id, ct) });

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct) { await _qr.RejectAsync(id, ct); return NoContent(); }

    [HttpGet("settings")]
    public async Task<ActionResult<QrOrderingSettingsDto>> Settings(CancellationToken ct) => Ok(await _qr.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<QrOrderingSettingsDto>> Save([FromBody] QrOrderingSettingsDto dto, CancellationToken ct) => Ok(await _qr.SaveSettingsAsync(dto, ct));

    [HttpPut("location")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<QrOrderingSettingsDto>> Location([FromBody] SetLocationBody body, CancellationToken ct) => Ok(await _qr.SetLocationAsync(body.Latitude, body.Longitude, ct));

    [HttpGet("codes")]
    public async Task<ActionResult<List<QrTableCodeDto>>> Codes(CancellationToken ct) => Ok(await _qr.GetTableCodesAsync(ct));

    [HttpPost("rotate-codes")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<List<QrTableCodeDto>>> Rotate(CancellationToken ct) => Ok(await _qr.RotateCodesAsync(ct));
}
