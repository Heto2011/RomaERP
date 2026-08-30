using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CashierShiftsController : ControllerBase
{
    private readonly ICashierShiftService _service;

    public CashierShiftsController(ICashierShiftService service)
    {
        _service = service;
    }

    [HttpGet("active")]
    public async Task<ActionResult<CashierShiftDto?>> GetActive([FromQuery] Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetActiveShiftAsync(employeeId, ct));

    [HttpPost("open")]
    public async Task<ActionResult<CashierShiftDto>> Open(OpenCashierShiftDto dto, CancellationToken ct)
        => Ok(await _service.OpenAsync(dto, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<CashierShiftDto>> Close(Guid id, CloseCashierShiftDto dto, CancellationToken ct)
        => Ok(await _service.CloseAsync(id, dto, ct));
}
