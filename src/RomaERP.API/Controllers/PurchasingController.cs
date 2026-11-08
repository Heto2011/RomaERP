using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RomaERP.Application.Common;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Purchasing.DTOs;
using RomaERP.Application.Purchasing.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.PurchasingPolicy)]
[Route("api/purchasing")]
public class PurchasingController : ControllerBase
{
    private readonly IPurchasingService _purchasingService;
    private readonly IInvoiceScanService _invoiceScanService;
    private readonly IPendingPurchaseService _pendingPurchases;

    public PurchasingController(IPurchasingService purchasingService, IInvoiceScanService invoiceScanService, IPendingPurchaseService pendingPurchases)
    {
        _purchasingService = purchasingService;
        _invoiceScanService = invoiceScanService;
        _pendingPurchases = pendingPurchases;
    }

    private bool CanApprove => User.IsInRole("Admin") || User.IsInRole("Accountant");
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty;
    private string CurrentUserName => User.FindFirstValue("fullName") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;

    [HttpGet("vendors")]
    public async Task<ActionResult<List<VendorDto>>> GetVendors(CancellationToken ct)
        => Ok(await _purchasingService.GetVendorsAsync(ct));

    [HttpPost("vendors")]
    public async Task<ActionResult<VendorDto>> CreateVendor(CreateVendorDto dto, CancellationToken ct)
        => Ok(await _purchasingService.CreateVendorAsync(dto, ct));

    [HttpPut("vendors/{id:guid}")]
    public async Task<ActionResult<VendorDto>> UpdateVendor(Guid id, UpdateVendorDto dto, CancellationToken ct)
        => Ok(await _purchasingService.UpdateVendorAsync(id, dto, ct));

    [HttpGet("invoices")]
    public async Task<ActionResult<List<PurchaseInvoiceDto>>> GetInvoices(CancellationToken ct)
        => Ok(await _purchasingService.GetInvoicesAsync(ct));

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<PurchaseInvoiceDto>> GetInvoice(Guid id, CancellationToken ct)
        => Ok(await _purchasingService.GetInvoiceAsync(id, ct));

    /// <summary>OCR a photo of a supplier invoice and return the fields to pre-fill the purchase invoice form. Nothing is saved.</summary>
    [HttpPost("invoices/scan")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<InvoiceScanResultDto>> ScanInvoice(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new RomaERP.Application.Common.Exceptions.ValidationAppException("الملف المرفوع فارغ.");

        var mediaType = file.ContentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            "image/gif" => "image/gif",
            _ => throw new RomaERP.Application.Common.Exceptions.ValidationAppException("صيغة الصورة غير مدعومة — لازم تكون JPEG أو PNG أو WEBP أو GIF.")
        };

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, ct);
        return Ok(await _invoiceScanService.ScanAsync(memoryStream.ToArray(), mediaType, ct));
    }

    /// <summary>Admins and Accountants post the invoice immediately. Anyone else who was given the Purchasing module only
    /// records it: it waits as a pending request (HTTP 202) and posts nothing until an approver accepts it.</summary>
    [HttpPost("invoices")]
    public async Task<ActionResult> CreateInvoice(CreatePurchaseInvoiceDto dto, CancellationToken ct)
    {
        if (CanApprove)
            return Ok(await _purchasingService.CreateInvoiceAsync(dto, ct));

        var pending = await _pendingPurchases.SubmitAsync(dto, CurrentUserId, CurrentUserName, ct);
        return Accepted(pending);
    }

    [HttpGet("invoices/pending")]
    public async Task<ActionResult<List<PendingPurchaseInvoiceDto>>> GetPending(CancellationToken ct)
        => Ok(await _pendingPurchases.ListAsync(CurrentUserId, CanApprove, ct));

    [HttpPost("invoices/pending/{id:guid}/approve")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<ActionResult<PurchaseInvoiceDto>> ApprovePending(Guid id, CancellationToken ct)
        => Ok(await _pendingPurchases.ApproveAsync(id, CurrentUserName, ct));

    [HttpPost("invoices/pending/{id:guid}/reject")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> RejectPending(Guid id, RejectPendingPurchaseDto dto, CancellationToken ct)
    {
        await _pendingPurchases.RejectAsync(id, CurrentUserName, dto.Reason, ct);
        return NoContent();
    }

    /// <summary>Item-based purchase receiving — used by the Restaurant "Purchase Receiving" screen as an
    /// internal-control record of what physically arrived. Updates stock quantity/cost per line only; it
    /// does not post a journal entry or touch the vendor's AP balance (that stays the accounting Purchase
    /// Invoices screen's job).</summary>
    [HttpPost("invoices/receive-inventory")]
    public async Task<ActionResult<InventoryReceiptDto>> ReceiveInventoryPurchase(ReceiveInventoryPurchaseDto dto, CancellationToken ct)
        => Ok(await _purchasingService.ReceiveInventoryPurchaseAsync(dto, ct));

    [HttpPost("invoices/{id:guid}/payments")]
    [Authorize(Roles = "Admin,Accountant")]
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
