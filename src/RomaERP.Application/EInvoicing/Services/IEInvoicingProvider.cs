using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services;

public record EInvoiceSubmissionResult(bool Success, string? ExternalUuid, string? DocumentHash, string? ErrorMessage);

/// <summary>One implementation per tax authority (Egypt's ETA, Saudi's ZATCA). EInvoicingService picks the
/// right one for the tenant based on CompanySettings.EInvoicingProvider.</summary>
public interface IEInvoicingProvider
{
    EInvoicingProvider ProviderType { get; }

    Task<EInvoiceSubmissionResult> SubmitInvoiceAsync(
        SalesInvoice invoice,
        Customer customer,
        CompanySettings settings,
        CancellationToken ct = default);
}
