using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Alerts.DTOs;
using RomaERP.Application.Alerts.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.ReportsPolicy)]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertsService _service;

    public AlertsController(IAlertsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<AlertsReportDto>> GetAlerts(CancellationToken ct)
        => Ok(await _service.GetAlertsAsync(ct));
}
