using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class FixedAssetsController : ControllerBase
{
    private readonly IFixedAssetService _fixedAssetService;

    public FixedAssetsController(IFixedAssetService fixedAssetService)
    {
        _fixedAssetService = fixedAssetService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FixedAssetDto>>> GetAll(CancellationToken ct)
        => Ok(await _fixedAssetService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FixedAssetDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _fixedAssetService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<FixedAssetDto>> Create(CreateFixedAssetDto dto, CancellationToken ct)
    {
        var result = await _fixedAssetService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
