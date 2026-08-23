using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class DepreciationController : ControllerBase
{
    private readonly IDepreciationService _depreciationService;

    public DepreciationController(IDepreciationService depreciationService)
    {
        _depreciationService = depreciationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DepreciationRunDto>>> GetAll(CancellationToken ct)
        => Ok(await _depreciationService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepreciationRunDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _depreciationService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<DepreciationRunDto>> Create(CreateDepreciationRunDto dto, CancellationToken ct)
    {
        var result = await _depreciationService.CreateAndCalculateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<DepreciationRunDto>> Post(Guid id, CancellationToken ct)
        => Ok(await _depreciationService.PostAsync(id, ct));
}
