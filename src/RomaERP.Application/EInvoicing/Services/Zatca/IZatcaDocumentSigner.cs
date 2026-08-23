using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

/// <summary>Applies ZATCA's required XAdES-BES enveloped signature (SHA256withECDSA over secp256k1) using the
/// tenant's CSID certificate/private key, and embeds the cryptographic stamp. Swap MockZatcaDocumentSigner for
/// a real implementation once a customer's CSID (from the CSR → Compliance CSID → Production CSID onboarding
/// flow) is available.</summary>
public interface IZatcaDocumentSigner
{
    Task<string> SignInvoiceXmlAsync(string invoiceXml, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in — appends a fake signature comment instead of a real XAdES signature.
/// Never use against a real (non-mock) ZATCA environment.</summary>
public class MockZatcaDocumentSigner : IZatcaDocumentSigner
{
    public Task<string> SignInvoiceXmlAsync(string invoiceXml, CompanySettings settings, CancellationToken ct = default)
    {
        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(invoiceXml)));
        return Task.FromResult($"{invoiceXml}\n<!-- MOCK-XADES-SIGNATURE:{hash} -->");
    }
}
