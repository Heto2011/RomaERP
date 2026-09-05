using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.EInvoicing;
using RomaERP.Infrastructure.EInvoicing.Zatca;
using Xunit;

namespace RomaERP.UnitTests;

public class ZatcaCertificateBuilderTests
{
    private static ZatcaCsrOptions ValidOptions() => new()
    {
        OrganizationIdentifier = "399999999900003",
        SolutionName = "RomaERP",
        Model = "SaaS",
        DeviceSerialNumber = "ed22f1d8-e6a2-1118-9b58-d9a8f11e445f",
        CommonName = "RomaERP Test Co",
        OrganizationName = "Roma Test Co",
        OrganizationalUnitName = "Riyadh Branch",
        Address = "Riyadh",
        BusinessCategory = "Software",
        Environment = EInvoicingEnvironment.Simulation,
    };

    [Fact]
    public void Generate_WithValidOptions_ProducesParsableCsrAndMatchingKey()
    {
        var result = ZatcaCertificateBuilder.Generate(ValidOptions());

        Assert.Contains("BEGIN CERTIFICATE REQUEST", result.CsrPem);
        Assert.Contains("BEGIN EC PRIVATE KEY", result.PrivateKeyPem);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(result.PrivateKeyPem);
        Assert.Equal("secp256k1", ecdsa.ExportParameters(false).Curve.Oid.FriendlyName, ignoreCase: true);
    }

    [Fact]
    public void Generate_CsrSubjectFollowsCouOCnOrder()
    {
        var result = ZatcaCertificateBuilder.Generate(ValidOptions());
        var der = ExtractDerFromPem(result.CsrPem);
        var request = CertificateRequest.LoadSigningRequest(der, HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.SkipSignatureValidation | CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);

        // The C→OU→O→CN insertion order this session read directly from ZATCA's reference implementation —
        // .NET's SubjectName.Name preserves it (its .Decode() helper reverses for display, but .Name doesn't).
        Assert.Equal("C=SA, OU=Riyadh Branch, O=Roma Test Co, CN=RomaERP Test Co", request.SubjectName.Name);
    }

    [Fact]
    public void Generate_IncludesCertificateTemplateExtensionMatchingEnvironment()
    {
        var prodOptions = ValidOptions();
        var result = ZatcaCertificateBuilder.Generate(new ZatcaCsrOptions
        {
            OrganizationIdentifier = prodOptions.OrganizationIdentifier,
            SolutionName = prodOptions.SolutionName,
            Model = prodOptions.Model,
            DeviceSerialNumber = prodOptions.DeviceSerialNumber,
            CommonName = prodOptions.CommonName,
            OrganizationName = prodOptions.OrganizationName,
            OrganizationalUnitName = prodOptions.OrganizationalUnitName,
            Address = prodOptions.Address,
            BusinessCategory = prodOptions.BusinessCategory,
            Environment = EInvoicingEnvironment.Production,
        });
        var der = ExtractDerFromPem(result.CsrPem);
        var request = CertificateRequest.LoadSigningRequest(der, HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.SkipSignatureValidation | CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);

        var templateExtension = request.CertificateExtensions.Single(e => e.Oid?.Value == "1.3.6.1.4.1.311.20.2");
        var templateText = System.Text.Encoding.UTF8.GetString(templateExtension.RawData);
        Assert.Contains("ZATCA-Code-Signing", templateText);
    }

    [Theory]
    [InlineData("199999999900003")] // doesn't start with 3
    [InlineData("39999999990000")] // 14 digits, doesn't end with 3
    [InlineData("3999999999000031")] // 16 digits
    public void Generate_RejectsInvalidOrganizationIdentifier(string badIdentifier)
    {
        var options = ValidOptions();
        var invalid = new ZatcaCsrOptions
        {
            OrganizationIdentifier = badIdentifier,
            SolutionName = options.SolutionName,
            Model = options.Model,
            DeviceSerialNumber = options.DeviceSerialNumber,
            CommonName = options.CommonName,
            OrganizationName = options.OrganizationName,
            OrganizationalUnitName = options.OrganizationalUnitName,
            Address = options.Address,
            BusinessCategory = options.BusinessCategory,
            Environment = options.Environment,
        };

        Assert.Throws<ValidationAppException>(() => ZatcaCertificateBuilder.Generate(invalid));
    }

    [Fact]
    public void Generate_RejectsForbiddenCharactersInOrganizationName()
    {
        var options = ValidOptions();
        var invalid = new ZatcaCsrOptions
        {
            OrganizationIdentifier = options.OrganizationIdentifier,
            SolutionName = options.SolutionName,
            Model = options.Model,
            DeviceSerialNumber = options.DeviceSerialNumber,
            CommonName = options.CommonName,
            OrganizationName = "Roma & Co <Test>",
            OrganizationalUnitName = options.OrganizationalUnitName,
            Address = options.Address,
            BusinessCategory = options.BusinessCategory,
            Environment = options.Environment,
        };

        Assert.Throws<ValidationAppException>(() => ZatcaCertificateBuilder.Generate(invalid));
    }

    [Fact]
    public void Generate_VatGroupRequiresTenDigitOrganizationalUnitName()
    {
        var options = ValidOptions();
        // 11th digit (index 10) == '1' marks a VAT group member.
        var vatGroupIdentifier = "310000000010003";
        Assert.Equal('1', vatGroupIdentifier[10]);

        var invalid = new ZatcaCsrOptions
        {
            OrganizationIdentifier = vatGroupIdentifier,
            SolutionName = options.SolutionName,
            Model = options.Model,
            DeviceSerialNumber = options.DeviceSerialNumber,
            CommonName = options.CommonName,
            OrganizationName = options.OrganizationName,
            OrganizationalUnitName = "NotTenDigits",
            Address = options.Address,
            BusinessCategory = options.BusinessCategory,
            Environment = options.Environment,
        };

        Assert.Throws<ValidationAppException>(() => ZatcaCertificateBuilder.Generate(invalid));
    }

    private static byte[] ExtractDerFromPem(string pem)
    {
        var base64 = pem
            .Replace("-----BEGIN CERTIFICATE REQUEST-----", "")
            .Replace("-----END CERTIFICATE REQUEST-----", "")
            .Trim();
        return Convert.FromBase64String(base64);
    }
}
