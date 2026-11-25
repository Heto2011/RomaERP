using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.PurchasingPolicy)]
[Route("api/purchasing/notes")]
public class PurchaseNotesController : ControllerBase
{
    private readonly IPurchaseNoteService _svc;
    public PurchaseNotesController(IPurchaseNoteService svc) { _svc = svc; }

    [HttpGet] public async Task<ActionResult<List<PurchaseNoteDto>>> GetAll(CancellationToken ct) => Ok(await _svc.GetAllAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult<PurchaseNoteDto>> Get(Guid id, CancellationToken ct) => Ok(await _svc.GetAsync(id, ct));
    [HttpPost] [Authorize(Roles = "Admin,Accountant")] public async Task<ActionResult<PurchaseNoteDto>> Create(CreatePurchaseNoteDto dto, CancellationToken ct) => Ok(await _svc.CreateAsync(dto, ct));
}
