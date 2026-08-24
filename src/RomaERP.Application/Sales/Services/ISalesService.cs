using RomaERP.Application.Sales.DTOs;

namespace RomaERP.Application.Sales.Services;

public interface ISalesService
{
    Task<List<CustomerDto>> GetCustomersAsync(CancellationToken ct = default);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, CancellationToken ct = default);

    Task<List<SalesInvoiceDto>> GetInvoicesAsync(CancellationToken ct = default);
    Task<SalesInvoiceDto> GetInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceDto dto, CancellationToken ct = default);
    Task<SalesInvoiceDto> RecordPaymentAsync(Guid invoiceId, RecordSalesPaymentDto dto, CancellationToken ct = default);
    Task<byte[]> GetInvoicePdfAsync(Guid id, CancellationToken ct = default);

    Task<List<CustomerAgingDto>> GetArAgingAsync(DateTime? asOfDate = null, CancellationToken ct = default);
}
