using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.PurchasingPolicy)]
[Route("api/purchasing/orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _svc;
    public PurchaseOrdersController(IPurchaseOrderService svc) { _svc = svc; }

    [HttpGet] public async Task<ActionResult<List<PurchaseOrderDto>>> GetAll(CancellationToken ct) => Ok(await _svc.GetAllAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult<PurchaseOrderDto>> Get(Guid id, CancellationToken ct) => Ok(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<ActionResult<PurchaseOrderDto>> Create(SavePurchaseOrderDto dto, CancellationToken ct) => Ok(await _svc.CreateAsync(dto, ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult<PurchaseOrderDto>> Update(Guid id, SavePurchaseOrderDto dto, CancellationToken ct) => Ok(await _svc.UpdateAsync(id, dto, ct));
    [HttpPost("{id:guid}/approve")] [Authorize(Roles = "Admin,Accountant")] public async Task<ActionResult<PurchaseOrderDto>> Approve(Guid id, CancellationToken ct) => Ok(await _svc.ApproveAsync(id, ct));
    [HttpPost("{id:guid}/cancel")] public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, CancellationToken ct) => Ok(await _svc.CancelAsync(id, ct));
    [HttpPost("{id:guid}/convert")] [Authorize(Roles = "Admin,Accountant")] public async Task<ActionResult<PurchaseOrderDto>> Convert(Guid id, ConvertPurchaseOrderDto dto, CancellationToken ct) => Ok(await _svc.ConvertAsync(id, dto, ct));
}
