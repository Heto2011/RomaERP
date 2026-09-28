using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.EInvoicing.Eta;
using Xunit;

namespace RomaERP.UnitTests;

public class EtaCertificateDocumentSignerTests
{
    private static (string CertificatePem, string PrivateKeyPem) CreateTestRsaCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Test Taxpayer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return (certificate.ExportCertificatePem(), rsa.ExportRSAPrivateKeyPem());
    }

    [Fact]
    public async Task SignInvoiceJsonAsync_WithoutStoredCertificate_Throws()
    {
        var signer = new EtaCertificateDocumentSigner(new PlainTextSecretProtector());
        var settings = new CompanySettings { CompanyNameAr = "شركة", CompanyNameEn = "Co" };

        await Assert.ThrowsAsync<ValidationAppException>(() => signer.SignInvoiceJsonAsync("{}", settings));
    }

    [Fact]
    public async Task SignInvoiceJsonAsync_EmbedsValidDetachedCmsSignatureOverOriginalContent()
    {
        var protector = new PlainTextSecretProtector();
        var (certificatePem, privateKeyPem) = CreateTestRsaCertificate();
        var settings = new CompanySettings
        {
            CompanyNameAr = "شركة",
            CompanyNameEn = "Co",
            EInvoicingCertificateEncrypted = protector.Protect(certificatePem),
            EInvoicingPrivateKeyEncrypted = protector.Protect(privateKeyPem),
        };
        var signer = new EtaCertificateDocumentSigner(protector);
        const string originalJson = """{"documentType":"I","internalId":"INV-1"}""";

        var signedJson = await signer.SignInvoiceJsonAsync(originalJson, settings);

        var document = JsonNode.Parse(signedJson)!.AsObject();
        Assert.Equal("I", document["documentType"]!.ToString());
        Assert.Equal("INV-1", document["internalId"]!.ToString());

        var signatureValue = document["signatures"]![0]!["value"]!.ToString();
        var signedCms = new SignedCms(new ContentInfo(System.Text.Encoding.UTF8.GetBytes(originalJson)), detached: true);
        signedCms.Decode(Convert.FromBase64String(signatureValue));
        signedCms.CheckSignature(verifySignatureOnly: true); // throws if the signature doesn't verify
    }
}
