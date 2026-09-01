using RomaERP.Application.Purchasing.DTOs;

namespace RomaERP.Application.Purchasing.Services;

public interface IPurchasingService
{
    Task<List<VendorDto>> GetVendorsAsync(CancellationToken ct = default);
    Task<VendorDto> CreateVendorAsync(CreateVendorDto dto, CancellationToken ct = default);

    Task<List<PurchaseInvoiceDto>> GetInvoicesAsync(CancellationToken ct = default);
    Task<PurchaseInvoiceDto> GetInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> CreateInvoiceAsync(CreatePurchaseInvoiceDto dto, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> ReceiveInventoryPurchaseAsync(ReceiveInventoryPurchaseDto dto, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> RecordPaymentAsync(Guid invoiceId, RecordPurchasePaymentDto dto, CancellationToken ct = default);
    Task<byte[]> GetInvoicePdfAsync(Guid id, CancellationToken ct = default);

    Task<List<VendorAgingDto>> GetApAgingAsync(DateTime? asOfDate = null, CancellationToken ct = default);
}
