using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Inventory.DTOs;
using RomaERP.Application.Inventory.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WasteEntriesController : ControllerBase
{
    private readonly IWasteEntryService _service;

    public WasteEntriesController(IWasteEntryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<WasteEntryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<WasteEntryDto>> Create(CreateWasteEntryDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));
}
