using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.Services.Eta;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.EInvoicing.Eta;

/// <summary>
/// Real ETA (Egyptian Tax Authority) invoice signer — a CAdES-BES / PKCS#7 (CMS) detached signature over the
/// invoice's canonical JSON, produced with the taxpayer's own certificate, embedded back into the document as
/// its <c>signatures</c> array before submission.
///
/// ETA's rules allow more than one certification path. The one most small/medium taxpayers use for the manual
/// portal is a hardware USB token (e.g. ePass2003) whose private key can never leave the token and therefore
/// cannot be used from a server process — that path genuinely cannot be automated here, hardware or no. This
/// signer instead targets ETA's API-based integration path, where the taxpayer registers for automated
/// submission and holds an ordinary certificate/private-key pair (the same PEM shape RomaERP already stores for
/// ZATCA) rather than a physical token. A taxpayer whose ETA registration ties them to the hardware-token path
/// instead cannot use this signer.
///
/// UNVERIFIED — same caveat as ZatcaXadesDocumentSigner: this session has no network access to eta.gov.eg and
/// no real ETA-issued certificate, so the exact canonical form ETA's compliance checker expects (JSON key
/// ordering, whitespace, digest algorithm choice) has not been confirmed against a live ETA environment.
/// Confirm against ETA's real preprod environment before relying on this in production.
/// </summary>
public class EtaCertificateDocumentSigner : IEtaDocumentSigner
{
    private readonly ISecretProtector _secretProtector;

    public EtaCertificateDocumentSigner(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public Task<string> SignInvoiceJsonAsync(string invoiceJson, CompanySettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.EInvoicingCertificateEncrypted) || string.IsNullOrWhiteSpace(settings.EInvoicingPrivateKeyEncrypted))
            throw new ValidationAppException("لازم ترفع شهادة (Certificate) ومفتاح خاص (Private Key) صادرين من مصلحة الضرائب المصرية (ETA) قبل إرسال أي فاتورة إلكترونية مصرية.");

        var certificatePem = _secretProtector.Unprotect(settings.EInvoicingCertificateEncrypted);
        var privateKeyPem = _secretProtector.Unprotect(settings.EInvoicingPrivateKeyEncrypted);

        using var certificateOnly = X509Certificate2.CreateFromPem(certificatePem);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        using var certificate = certificateOnly.CopyWithPrivateKey(rsa);

        var contentBytes = Encoding.UTF8.GetBytes(invoiceJson);
        var signedCms = new SignedCms(new ContentInfo(contentBytes), detached: true);
        var signer = new CmsSigner(certificate) { DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1") }; // SHA-256
        signedCms.ComputeSignature(signer);
        var signatureBase64 = Convert.ToBase64String(signedCms.Encode());

        var document = JsonNode.Parse(invoiceJson)!.AsObject();
        document["signatures"] = new JsonArray(new JsonObject
        {
            ["signatureType"] = "I",
            ["value"] = signatureBase64,
        });

        return Task.FromResult(document.ToJsonString());
    }
}
