using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.POSPolicy)]
[Route("api/delivery-platforms")]
public class DeliveryPlatformsController : ControllerBase
{
    private readonly IDeliveryOrderIntakeService _intakeService;

    public DeliveryPlatformsController(IDeliveryOrderIntakeService intakeService)
    {
        _intakeService = intakeService;
    }

    [HttpGet("status")]
    public ActionResult<List<DeliveryPlatformStatusDto>> GetStatus()
        => Ok(_intakeService.GetPlatformStatuses());

    [HttpGet("webhook-events")]
    public async Task<ActionResult<List<DeliveryWebhookEventDto>>> GetEvents(CancellationToken ct)
        => Ok(await _intakeService.GetEventsAsync(ct));

    [HttpPost("webhook-events/{id:guid}/retry")]
    public async Task<ActionResult<DeliveryWebhookEventDto>> Retry(Guid id, CancellationToken ct)
        => Ok(await _intakeService.RetryAsync(id, ct));

    [HttpGet("item-mappings")]
    public async Task<ActionResult<List<DeliveryPlatformItemMappingDto>>> GetItemMappings([FromQuery] string? platformName, CancellationToken ct)
        => Ok(await _intakeService.GetItemMappingsAsync(platformName, ct));

    [HttpPost("item-mappings")]
    public async Task<ActionResult<DeliveryPlatformItemMappingDto>> SetItemMapping(SaveDeliveryPlatformItemMappingDto dto, CancellationToken ct)
        => Ok(await _intakeService.SetItemMappingAsync(dto, ct));

    [HttpDelete("item-mappings/{id:guid}")]
    public async Task<IActionResult> DeleteItemMapping(Guid id, CancellationToken ct)
    {
        await _intakeService.DeleteItemMappingAsync(id, ct);
        return NoContent();
    }
}
