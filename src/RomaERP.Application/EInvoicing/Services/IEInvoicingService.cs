using RomaERP.Application.EInvoicing.DTOs;

namespace RomaERP.Application.EInvoicing.Services;

public interface IEInvoicingService
{
    Task<EInvoicingSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<EInvoicingSettingsDto> UpdateSettingsAsync(UpdateEInvoicingSettingsDto dto, CancellationToken ct = default);
    Task<EInvoiceStatusDto> SubmitInvoiceAsync(Guid salesInvoiceId, CancellationToken ct = default);
}
