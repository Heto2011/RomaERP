using System.Xml.Linq;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

public class ZatcaEInvoicingProvider : IEInvoicingProvider
{
    /// <summary>ZATCA's documented base64 placeholder for the very first invoice's PIH (SHA-256 of "0") —
    /// confirm against ZATCA's official documentation before relying on it in production.</summary>
    public const string FirstInvoicePih = "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==";

    private readonly IZatcaDocumentSigner _signer;
    private readonly IZatcaApiClient _apiClient;

    public ZatcaEInvoicingProvider(IZatcaDocumentSigner signer, IZatcaApiClient apiClient)
    {
        _signer = signer;
        _apiClient = apiClient;
    }

    public EInvoicingProvider ProviderType => EInvoicingProvider.Zatca;

    public async Task<EInvoiceSubmissionResult> SubmitInvoiceAsync(
        SalesInvoice invoice, Customer customer, CompanySettings settings, CancellationToken ct = default)
    {
        var invoiceType = ZatcaInvoiceDocumentBuilder.DetermineInvoiceType(customer);
        var icv = settings.EInvoicingSubmittedCount + 1;
        var pih = string.IsNullOrEmpty(settings.EInvoicingLastInvoiceHash) ? FirstInvoicePih : settings.EInvoicingLastInvoiceHash;

        var (unsignedDocument, _) = ZatcaInvoiceDocumentBuilder.Build(invoice, customer, settings, icv, pih);
        var unsignedXml = unsignedDocument.ToString(SaveOptions.DisableFormatting);

        var signingResult = await _signer.SignInvoiceXmlAsync(unsignedXml, settings, ct);

        var response = invoiceType == ZatcaInvoiceType.Standard
            ? await _apiClient.ClearStandardInvoiceAsync(signingResult.SignedXml, signingResult.InvoiceHash, signingResult.Uuid, settings, ct)
            : await _apiClient.ReportSimplifiedInvoiceAsync(signingResult.SignedXml, signingResult.InvoiceHash, signingResult.Uuid, settings, ct);

        if (response.Success)
        {
            settings.EInvoicingSubmittedCount = icv;
            settings.EInvoicingLastInvoiceHash = signingResult.InvoiceHash;
        }

        return new EInvoiceSubmissionResult(response.Success, invoice.Id.ToString(), signingResult.InvoiceHash, response.ErrorMessage);
    }
}
