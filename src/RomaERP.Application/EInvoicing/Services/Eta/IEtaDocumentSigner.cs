using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Eta;

/// <summary>Signs the ETA invoice JSON with the taxpayer's certificate. See
/// RomaERP.Infrastructure.EInvoicing.Eta.EtaCertificateDocumentSigner for the real (CAdES/PKCS#7) implementation
/// and its caveats — it covers ETA's API-based certificate registration path only. A taxpayer whose ETA
/// registration instead requires the hardware-USB-token manual-portal path (PKCS#11, e.g. ePass2003) cannot be
/// automated from a server process at all, and needs a different integration entirely.</summary>
public interface IEtaDocumentSigner
{
    Task<string> SignInvoiceJsonAsync(string invoiceJson, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in — does not perform real cryptographic signing. Only used by unit tests
/// exercising submission orchestration — the real EtaCertificateDocumentSigner is what's registered in
/// production (see RomaERP.Infrastructure.DependencyInjection).</summary>
public class MockEtaDocumentSigner : IEtaDocumentSigner
{
    public Task<string> SignInvoiceJsonAsync(string invoiceJson, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult($"MOCK-SIGNATURE:{Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(invoiceJson)))}");
}
