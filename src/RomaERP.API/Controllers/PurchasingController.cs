using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/purchasing")]
public class PurchasingController : ControllerBase
{
    private readonly IPurchasingService _purchasingService;

    public PurchasingController(IPurchasingService purchasingService)
    {
        _purchasingService = purchasingService;
    }

    [HttpGet("vendors")]
    public async Task<ActionResult<List<VendorDto>>> GetVendors(CancellationToken ct)
        => Ok(await _purchasingService.GetVendorsAsync(ct));

    [HttpPost("vendors")]
    public async Task<ActionResult<VendorDto>> CreateVendor(CreateVendorDto dto, CancellationToken ct)
        => Ok(await _purchasingService.CreateVendorAsync(dto, ct));

    [HttpGet("invoices")]
    public async Task<ActionResult<List<PurchaseInvoiceDto>>> GetInvoices(CancellationToken ct)
        => Ok(await _purchasingService.GetInvoicesAsync(ct));

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<PurchaseInvoiceDto>> GetInvoice(Guid id, CancellationToken ct)
        => Ok(await _purchasingService.GetInvoiceAsync(id, ct));

    [HttpPost("invoices")]
    public async Task<ActionResult<PurchaseInvoiceDto>> CreateInvoice(CreatePurchaseInvoiceDto dto, CancellationToken ct)
        => Ok(await _purchasingService.CreateInvoiceAsync(dto, ct));

    [HttpPost("invoices/{id:guid}/payments")]
    public async Task<ActionResult<PurchaseInvoiceDto>> RecordPayment(Guid id, RecordPurchasePaymentDto dto, CancellationToken ct)
        => Ok(await _purchasingService.RecordPaymentAsync(id, dto, ct));
}
