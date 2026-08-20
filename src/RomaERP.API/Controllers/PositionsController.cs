using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PositionsController : ControllerBase
{
    private readonly IPositionService _positionService;

    public PositionsController(IPositionService positionService)
    {
        _positionService = positionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PositionDto>>> GetAll(CancellationToken ct)
        => Ok(await _positionService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PositionDto>> Create(CreatePositionDto dto, CancellationToken ct)
        => Ok(await _positionService.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<PositionDto>> Update(Guid id, CreatePositionDto dto, CancellationToken ct)
        => Ok(await _positionService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _positionService.DeleteAsync(id, ct);
        return NoContent();
    }
}
