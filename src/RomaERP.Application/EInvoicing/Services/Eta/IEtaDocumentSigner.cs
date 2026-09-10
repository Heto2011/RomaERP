using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Eta;

/// <summary>Signs the ETA invoice JSON with the taxpayer's USB token (PKCS#11, e.g. ePass2003) — ETA requires
/// a real hardware token or a local signing agent; this cannot be done from a server process alone. Swap
/// MockEtaDocumentSigner for a real implementation once a customer's token/signing agent is available.</summary>
public interface IEtaDocumentSigner
{
    Task<string> SignInvoiceJsonAsync(string invoiceJson, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in — does not perform real cryptographic signing. Never use against a real
/// (non-mock) ETA environment.</summary>
public class MockEtaDocumentSigner : IEtaDocumentSigner
{
    public Task<string> SignInvoiceJsonAsync(string invoiceJson, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult($"MOCK-SIGNATURE:{Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(invoiceJson)))}");
}
