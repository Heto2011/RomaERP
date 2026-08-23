using System.Security.Cryptography;
using System.Text;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Eta;

public class EtaEInvoicingProvider : IEInvoicingProvider
{
    private readonly IEtaDocumentSigner _signer;
    private readonly IEtaApiClient _apiClient;

    public EtaEInvoicingProvider(IEtaDocumentSigner signer, IEtaApiClient apiClient)
    {
        _signer = signer;
        _apiClient = apiClient;
    }

    public EInvoicingProvider ProviderType => EInvoicingProvider.Eta;

    public async Task<EInvoiceSubmissionResult> SubmitInvoiceAsync(
        SalesInvoice invoice, Customer customer, CompanySettings settings, CancellationToken ct = default)
    {
        var document = EtaInvoiceDocumentBuilder.Build(invoice, customer, settings);
        var json = document.ToJsonString();

        var signed = await _signer.SignInvoiceJsonAsync(json, settings, ct);
        var response = await _apiClient.SubmitSignedDocumentAsync(signed, settings, ct);

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return new EInvoiceSubmissionResult(response.Accepted, response.Uuid, hash, response.ErrorMessage);
    }
}
