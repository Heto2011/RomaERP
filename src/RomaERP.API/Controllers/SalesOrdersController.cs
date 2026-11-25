using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.SalesPolicy)]
[Route("api/sales/orders")]
public class SalesOrdersController : ControllerBase
{
    private readonly ISalesOrderService _svc;
    private readonly ICurrentUserService _user;
    public SalesOrdersController(ISalesOrderService svc, ICurrentUserService user) { _svc = svc; _user = user; }

    [HttpGet] public async Task<ActionResult<List<SalesOrderDto>>> GetAll(CancellationToken ct) => Ok(await _svc.GetAllAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult<SalesOrderDto>> Get(Guid id, CancellationToken ct) => Ok(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<ActionResult<SalesOrderDto>> Create(SaveSalesOrderDto dto, CancellationToken ct) => Ok(await _svc.CreateAsync(dto, ct));
    [HttpPost("from-quotation/{quotationId:guid}")] public async Task<ActionResult<SalesOrderDto>> FromQuotation(Guid quotationId, CancellationToken ct) => Ok(await _svc.CreateFromQuotationAsync(quotationId, ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult<SalesOrderDto>> Update(Guid id, SaveSalesOrderDto dto, CancellationToken ct) => Ok(await _svc.UpdateAsync(id, dto, ct));
    [HttpPost("{id:guid}/confirm")] public async Task<ActionResult<SalesOrderDto>> Confirm(Guid id, CancellationToken ct) => Ok(await _svc.ConfirmAsync(id, ct));
    [HttpPost("{id:guid}/delivered")] public async Task<ActionResult<SalesOrderDto>> Delivered(Guid id, CancellationToken ct) => Ok(await _svc.MarkDeliveredAsync(id, _user.UserName, ct));
    [HttpPost("{id:guid}/cancel")] public async Task<ActionResult<SalesOrderDto>> Cancel(Guid id, CancellationToken ct) => Ok(await _svc.CancelAsync(id, ct));
    [HttpPost("{id:guid}/convert")] [Authorize(Roles = "Admin,Accountant")] public async Task<ActionResult<SalesOrderDto>> Convert(Guid id, ConvertSalesOrderDto dto, CancellationToken ct) => Ok(await _svc.ConvertAsync(id, dto, ct));
}
