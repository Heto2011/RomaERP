using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.EInvoicing.DTOs;
using RomaERP.Application.EInvoicing.Services;
using RomaERP.Application.Sales.DTOs;
using RomaERP.Application.Sales.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;
    private readonly IEInvoicingService _eInvoicingService;

    public SalesController(ISalesService salesService, IEInvoicingService eInvoicingService)
    {
        _salesService = salesService;
        _eInvoicingService = eInvoicingService;
    }

    [HttpGet("customers")]
    public async Task<ActionResult<List<CustomerDto>>> GetCustomers(CancellationToken ct)
        => Ok(await _salesService.GetCustomersAsync(ct));

    [HttpPost("customers")]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(CreateCustomerDto dto, CancellationToken ct)
        => Ok(await _salesService.CreateCustomerAsync(dto, ct));

    [HttpGet("invoices")]
    public async Task<ActionResult<List<SalesInvoiceDto>>> GetInvoices(CancellationToken ct)
        => Ok(await _salesService.GetInvoicesAsync(ct));

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<SalesInvoiceDto>> GetInvoice(Guid id, CancellationToken ct)
        => Ok(await _salesService.GetInvoiceAsync(id, ct));

    [HttpGet("invoices/{id:guid}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid id, CancellationToken ct)
    {
        var pdfBytes = await _salesService.GetInvoicePdfAsync(id, ct);
        return File(pdfBytes, "application/pdf", $"invoice-{id}.pdf");
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<SalesInvoiceDto>> CreateInvoice(CreateSalesInvoiceDto dto, CancellationToken ct)
        => Ok(await _salesService.CreateInvoiceAsync(dto, ct));

    [HttpPost("invoices/{id:guid}/payments")]
    public async Task<ActionResult<SalesInvoiceDto>> RecordPayment(Guid id, RecordSalesPaymentDto dto, CancellationToken ct)
        => Ok(await _salesService.RecordPaymentAsync(id, dto, ct));

    [HttpGet("aging")]
    public async Task<ActionResult<List<CustomerAgingDto>>> GetAging(DateTime? asOfDate, CancellationToken ct)
        => Ok(await _salesService.GetArAgingAsync(asOfDate, ct));

    [HttpPost("invoices/{id:guid}/submit-einvoice")]
    public async Task<ActionResult<EInvoiceStatusDto>> SubmitEInvoice(Guid id, CancellationToken ct)
        => Ok(await _eInvoicingService.SubmitInvoiceAsync(id, ct));
}
