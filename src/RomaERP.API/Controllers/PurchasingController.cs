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

    /// <summary>Item-based purchase receiving — used by the Restaurant "Purchase Receiving" screen as an
    /// internal-control record of what physically arrived. Updates stock quantity/cost per line only; it
    /// does not post a journal entry or touch the vendor's AP balance (that stays the accounting Purchase
    /// Invoices screen's job).</summary>
    [HttpPost("invoices/receive-inventory")]
    public async Task<ActionResult<InventoryReceiptDto>> ReceiveInventoryPurchase(ReceiveInventoryPurchaseDto dto, CancellationToken ct)
        => Ok(await _purchasingService.ReceiveInventoryPurchaseAsync(dto, ct));

    [HttpPost("invoices/{id:guid}/payments")]
    public async Task<ActionResult<PurchaseInvoiceDto>> RecordPayment(Guid id, RecordPurchasePaymentDto dto, CancellationToken ct)
        => Ok(await _purchasingService.RecordPaymentAsync(id, dto, ct));

    [HttpGet("invoices/{id:guid}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid id, CancellationToken ct)
    {
        var pdfBytes = await _purchasingService.GetInvoicePdfAsync(id, ct);
        return File(pdfBytes, "application/pdf", $"purchase-invoice-{id}.pdf");
    }

    [HttpGet("aging")]
    public async Task<ActionResult<List<VendorAgingDto>>> GetAging(DateTime? asOfDate, CancellationToken ct)
        => Ok(await _purchasingService.GetApAgingAsync(asOfDate, ct));
}
