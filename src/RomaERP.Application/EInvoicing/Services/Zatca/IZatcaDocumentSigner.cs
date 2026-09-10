using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

/// <summary>SignedXml: the final UBL XML with ext:UBLExtensions (XAdES-BES signature), the QR
/// AdditionalDocumentReference, and the cac:Signature marker inserted. InvoiceHash: the base64 SHA-256 digest
/// of the canonicalized "unsigned" document — this becomes the PIH (Previous Invoice Hash) chained into the
/// next invoice, so it must be returned even though it's also embedded inside SignedXml's QR code. Uuid: the
/// invoice's cbc:UUID, needed alongside InvoiceHash when submitting to ZATCA's Reporting/Clearance APIs.</summary>
public record ZatcaSigningResult(string SignedXml, string InvoiceHash, string Uuid);

/// <summary>Applies ZATCA's required XAdES-BES enveloped signature (ECDSA over secp256k1, SHA-256) using the
/// tenant's CSID certificate/private key, and embeds the cryptographic-stamp QR code. See
/// RomaERP.Infrastructure.EInvoicing.Zatca.ZatcaXadesDocumentSigner for the real implementation and its
/// documented caveats.</summary>
public interface IZatcaDocumentSigner
{
    Task<ZatcaSigningResult> SignInvoiceXmlAsync(string unsignedInvoiceXml, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in — used only where no real ZATCA certificate is configured (e.g. unit
/// tests exercising the submission flow rather than the cryptography itself). Never registered against a real
/// (non-mock) ZATCA environment — see DependencyInjection for the real ZatcaXadesDocumentSigner registration.</summary>
public class MockZatcaDocumentSigner : IZatcaDocumentSigner
{
    public Task<ZatcaSigningResult> SignInvoiceXmlAsync(string unsignedInvoiceXml, CompanySettings settings, CancellationToken ct = default)
    {
        var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(unsignedInvoiceXml)));
        var signed = $"{unsignedInvoiceXml}\n<!-- MOCK-XADES-SIGNATURE:{hash} -->";
        var uuid = System.Xml.Linq.XDocument.Parse(unsignedInvoiceXml).Root?.Element(ZatcaInvoiceDocumentBuilder.Cbc + "UUID")?.Value ?? Guid.NewGuid().ToString();
        return Task.FromResult(new ZatcaSigningResult(signed, hash, uuid));
    }
}
