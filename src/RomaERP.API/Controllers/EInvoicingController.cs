using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.EInvoicing.DTOs;
using RomaERP.Application.EInvoicing.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/einvoicing")]
public class EInvoicingController : ControllerBase
{
    private readonly IEInvoicingService _eInvoicingService;

    public EInvoicingController(IEInvoicingService eInvoicingService)
    {
        _eInvoicingService = eInvoicingService;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<EInvoicingSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _eInvoicingService.GetSettingsAsync(ct));

    [HttpPut("settings")]
    public async Task<ActionResult<EInvoicingSettingsDto>> UpdateSettings(UpdateEInvoicingSettingsDto dto, CancellationToken ct)
        => Ok(await _eInvoicingService.UpdateSettingsAsync(dto, ct));
}
