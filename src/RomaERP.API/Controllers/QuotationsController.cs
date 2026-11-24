using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.SalesPolicy)]
[Route("api/sales/quotations")]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _svc;
    public QuotationsController(IQuotationService svc) { _svc = svc; }

    [HttpGet] public async Task<ActionResult<List<QuotationDto>>> GetAll(CancellationToken ct) => Ok(await _svc.GetAllAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult<QuotationDto>> Get(Guid id, CancellationToken ct) => Ok(await _svc.GetAsync(id, ct));
    [HttpPost] public async Task<ActionResult<QuotationDto>> Create(SaveQuotationDto dto, CancellationToken ct) => Ok(await _svc.CreateAsync(dto, ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult<QuotationDto>> Update(Guid id, SaveQuotationDto dto, CancellationToken ct) => Ok(await _svc.UpdateAsync(id, dto, ct));
    [HttpPost("{id:guid}/status")] public async Task<ActionResult<QuotationDto>> SetStatus(Guid id, SetQuotationStatusDto dto, CancellationToken ct) => Ok(await _svc.SetStatusAsync(id, dto.Status, ct));
    [HttpPost("{id:guid}/convert")] public async Task<ActionResult<QuotationDto>> Convert(Guid id, ConvertQuotationDto dto, CancellationToken ct) => Ok(await _svc.ConvertAsync(id, dto, ct));
}
