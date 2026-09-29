using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Domain.Sales;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.EInvoicing.Zatca;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Exercises the real ZATCA signer (RomaERP.Infrastructure.EInvoicing.Zatca.ZatcaXadesDocumentSigner)
/// against a locally generated, self-signed secp256k1 test certificate — NOT a real ZATCA-issued CSID. These
/// tests can only confirm the implementation is internally consistent (the signature verifies, the hash is
/// stable, the QR/XAdES structure is present) — they cannot confirm ZATCA's own compliance checker would
/// accept the output, since no real CSID or network access to ZATCA exists in this environment. See the
/// caveats documented on ZatcaXadesDocumentSigner itself before relying on this for production.</summary>
public class ZatcaXadesDocumentSignerTests
{
    private static readonly XNamespace Cac = ZatcaInvoiceDocumentBuilder.Cac;
    private static readonly XNamespace Cbc = ZatcaInvoiceDocumentBuilder.Cbc;
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private static (string CertPem, string KeyPem, X509Certificate2 Certificate) CreateTestCertificate()
    {
        var curve = ECCurve.CreateFromOid(new Oid("1.3.132.0.10", "secp256k1"));
        using var ecdsa = ECDsa.Create(curve);
        var request = new CertificateRequest("CN=RomaERP Test Seller, O=Roma Test Co, C=SA", ecdsa, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var certPem = certificate.ExportCertificatePem();
        var keyPem = ecdsa.ExportECPrivateKeyPem();
        return (certPem, keyPem, X509Certificate2.CreateFromPem(certPem));
    }

    private static CompanySettings SaudiSettingsWithCredentials(PlainTextSecretProtector protector, string certPem, string keyPem) => new()
    {
        CompanyNameAr = "شركة روما التجريبية",
        CompanyNameEn = "Roma Test Co",
        Country = Country.SaudiArabia,
        TaxRegistrationNumber = "300987654300003",
        VatRate = 0.15m,
        DefaultCurrency = "SAR",
        EInvoicingCertificateEncrypted = protector.Protect(certPem),
        EInvoicingPrivateKeyEncrypted = protector.Protect(keyPem),
    };

    private static SalesInvoice NewInvoice(Customer customer)
    {
        const decimal subTotal = 1000m;
        const decimal vatRate = 0.15m;
        var vat = subTotal * vatRate;
        return new SalesInvoice
        {
            InvoiceNumber = "SI-000001",
            InvoiceDate = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc),
            Customer = customer,
            CustomerId = customer.Id,
            SubTotal = subTotal,
            VatRate = vatRate,
            VatAmount = vat,
            TotalAmount = subTotal + vat,
            Lines = new List<SalesInvoiceLine>
            {
                new() { LineNumber = 1, Description = "Consulting", Quantity = 1, UnitPrice = subTotal, LineTotal = subTotal }
            }
        };
    }

    /// <summary>Independently recomputes the canonical invoice hash the same way the signer's own
    /// ComputeCanonicalSha256 does, so the test isn't just asserting against the implementation's own output.</summary>
    private static byte[] RecomputeCanonicalHash(string unsignedXml)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(unsignedXml);
        var transform = new XmlDsigC14NTransform(includeComments: false);
        transform.LoadInput(doc);
        using var output = (Stream)transform.GetOutput(typeof(Stream));
        using var buffer = new MemoryStream();
        output.CopyTo(buffer);
        return SHA256.HashData(buffer.ToArray());
    }

    [Fact]
    public async Task SignInvoiceXmlAsync_ProducesSignatureThatVerifiesAgainstTheCertificatesPublicKey()
    {
        var (certPem, keyPem, certificate) = CreateTestCertificate();
        var protector = new PlainTextSecretProtector();
        var settings = SaudiSettingsWithCredentials(protector, certPem, keyPem);
        var customer = new Customer { Code = "CUST-1", NameAr = "شركة تجريبية", NameEn = "Test Co", TaxRegistrationNumber = "300123456700003" };
        var invoice = NewInvoice(customer);

        var (unsignedDocument, _) = ZatcaInvoiceDocumentBuilder.Build(invoice, customer, settings, invoiceCounterValue: 1, previousInvoiceHash: ZatcaEInvoicingProvider.FirstInvoicePih);
        var unsignedXml = unsignedDocument.ToString(SaveOptions.DisableFormatting);

        var signer = new ZatcaXadesDocumentSigner(protector);
        var result = await signer.SignInvoiceXmlAsync(unsignedXml, settings);

        // The returned invoice hash must match an independent recomputation of the same canonicalization.
        var expectedHash = RecomputeCanonicalHash(unsignedXml);
        Assert.Equal(Convert.ToBase64String(expectedHash), result.InvoiceHash);

        // The embedded ds:SignatureValue must cryptographically verify against that hash using the
        // certificate's own public key — this is the core correctness check: an invalid signature or wrong
        // digest algorithm would fail here regardless of any ZATCA-specific formatting details around it.
        var signedDocument = XDocument.Parse(result.SignedXml);
        var signatureValueBase64 = signedDocument.Descendants(Ds + "SignatureValue").Single().Value;
        var signatureBytes = Convert.FromBase64String(signatureValueBase64);

        using var publicEcdsa = certificate.GetECDsaPublicKey();
        Assert.NotNull(publicEcdsa);
        var verified = publicEcdsa!.VerifyData(expectedHash, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Assert.True(verified, "ECDSA signature does not verify against the invoice hash and certificate public key.");
    }

    [Fact]
    public async Task SignInvoiceXmlAsync_EmbedsQrCodeWithMatchingHashAndSignature()
    {
        var (certPem, keyPem, _) = CreateTestCertificate();
        var protector = new PlainTextSecretProtector();
        var settings = SaudiSettingsWithCredentials(protector, certPem, keyPem);
        var customer = new Customer { Code = "CUST-1", NameAr = "شركة تجريبية", NameEn = "Test Co", TaxRegistrationNumber = "300123456700003" };
        var invoice = NewInvoice(customer);

        var (unsignedDocument, _) = ZatcaInvoiceDocumentBuilder.Build(invoice, customer, settings, invoiceCounterValue: 1, previousInvoiceHash: ZatcaEInvoicingProvider.FirstInvoicePih);
        var signer = new ZatcaXadesDocumentSigner(protector);
        var result = await signer.SignInvoiceXmlAsync(unsignedDocument.ToString(SaveOptions.DisableFormatting), settings);

        var signedDocument = XDocument.Parse(result.SignedXml);
        var qrNode = signedDocument.Descendants(Cac + "AdditionalDocumentReference")
            .Single(e => (string?)e.Element(Cbc + "ID") == "QR");
        var qrBase64 = qrNode.Descendants(Cbc + "EmbeddedDocumentBinaryObject").Single().Value;

        var bytes = Convert.FromBase64String(qrBase64);
        var fields = new Dictionary<byte, byte[]>();
        var i = 0;
        while (i < bytes.Length)
        {
            var tag = bytes[i];
            var len = bytes[i + 1];
            fields[tag] = bytes[(i + 2)..(i + 2 + len)];
            i += 2 + len;
        }

        // Tag 6 (invoice hash) must be the same base64 hash returned alongside the signed XML.
        Assert.Equal(result.InvoiceHash, System.Text.Encoding.UTF8.GetString(fields[6]));
        // Standard (B2B) invoice: no tag 9 (certificate signature is Simplified/B2C-only).
        Assert.False(fields.ContainsKey(9));
        // Seller name (tag 1) round-trips from CompanySettings.
        Assert.Equal(settings.CompanyNameAr, System.Text.Encoding.UTF8.GetString(fields[1]));
    }

    [Fact]
    public async Task SignInvoiceXmlAsync_WithoutStoredCredentials_Throws()
    {
        var protector = new PlainTextSecretProtector();
        var settings = new CompanySettings
        {
            CompanyNameAr = "شركة بدون شهادة",
            CompanyNameEn = "No Cert Co",
            Country = Country.SaudiArabia,
            VatRate = 0.15m,
            DefaultCurrency = "SAR",
        };
        var signer = new ZatcaXadesDocumentSigner(protector);

        await Assert.ThrowsAsync<RomaERP.Application.Common.Exceptions.ValidationAppException>(
            () => signer.SignInvoiceXmlAsync("<Invoice xmlns=\"urn:x\"></Invoice>", settings));
    }
}
