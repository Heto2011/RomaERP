using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Application.Restaurant.Services;
using RomaERP.Domain.Restaurant;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.POSPolicy)]
[Route("api/restaurant/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _service;

    public ReservationsController(IReservationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ReservationDto>>> Get(DateTime? date, ReservationStatus? status, CancellationToken ct)
        => Ok(await _service.GetAsync(date, status, ct));

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(SaveReservationDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReservationDto>> Update(Guid id, SaveReservationDto dto, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));

    [HttpPost("{id:guid}/no-show")]
    public async Task<ActionResult<ReservationDto>> NoShow(Guid id, CancellationToken ct)
        => Ok(await _service.MarkNoShowAsync(id, ct));

    /// <summary>The guests arrived: turn the reservation into a running order (dine-in or takeaway).</summary>
    [HttpPost("{id:guid}/seat")]
    public async Task<ActionResult<SeatReservationResultDto>> Seat(Guid id, SeatReservationDto dto, CancellationToken ct)
        => Ok(await _service.SeatAsync(id, dto, ct));
}
