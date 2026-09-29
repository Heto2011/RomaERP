using System.Formats.Asn1;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.EInvoicing.Zatca;

/// <summary>
/// Real ZATCA XAdES-BES invoice signer — ECDSA secp256k1 / SHA-256, XML-DSig enveloped inside
/// ext:UBLExtensions, with the 8/9-tag production QR code (cryptographic stamp).
///
/// The exact algorithm (which nodes get excluded before hashing, the two-reference SignedInfo shape, the
/// literal SignedProperties string used for its own digest, which fields feed the QR) was ported from the
/// publicly available open-source library "Saleh7/php-zatca-xml" (MIT-licensed, read from GitHub during this
/// session), since RomaERP has no official ZATCA reference documentation and no live network access to ZATCA's
/// endpoints or compliance checker to derive/verify this independently.
///
/// KNOWN OPEN QUESTIONS — this has NOT been validated against a real ZATCA-issued certificate or ZATCA's
/// compliance checker, and must be run through the official CSR → Compliance CSID → 6-invoice compliance test
/// flow before any production use:
///   1. Digest quirk: the reference computes xades:CertDigest and the SignedProperties ds:Reference digest as
///      base64(hex-string-of-sha256(x)) rather than the standard base64(raw-sha256-bytes(x)) — i.e. it hashes,
///      then hex-encodes, then base64-encodes the hex text. This looks like it could be an accidental bug in
///      the reference (the "real" invoice hash used for signing/PIH does NOT have this quirk), but since it's
///      applied consistently in two places there, and there is no way to verify which ZATCA actually expects,
///      this signer replicates it faithfully rather than "fixing" it on a guess. See QuirkyDigest() below.
///   2. ds:X509Certificate content: implemented here as standard base64(DER) per XML-DSig, NOT the raw PEM
///      text the reference appears to embed — this one intentional deviation follows the XML-DSig spec rather
///      than the reference, since embedding PEM armor there would be non-conformant XML-DSig regardless of ZATCA.
///   3. X509IssuerName formatting (reversed, comma-joined DN) and the C14N version (this uses .NET's built-in
///      C14N 1.0 transform; ZATCA's algorithm identifier is technically C14N 1.1 — the two only differ on
///      edge cases this invoice XML doesn't exercise, but it hasn't been proven identical for ZATCA's checker).
/// </summary>
public class ZatcaXadesDocumentSigner : IZatcaDocumentSigner
{
    private static readonly XNamespace Cac = ZatcaInvoiceDocumentBuilder.Cac;
    private static readonly XNamespace Cbc = ZatcaInvoiceDocumentBuilder.Cbc;
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private static readonly XNamespace Sig = "urn:oasis:names:specification:ubl:schema:xsd:CommonSignatureComponents-2";
    private static readonly XNamespace Sac = "urn:oasis:names:specification:ubl:schema:xsd:SignatureAggregateComponents-2";
    private static readonly XNamespace Sbc = "urn:oasis:names:specification:ubl:schema:xsd:SignatureBasicComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";

    private readonly ISecretProtector _secretProtector;

    public ZatcaXadesDocumentSigner(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public Task<ZatcaSigningResult> SignInvoiceXmlAsync(string unsignedInvoiceXml, CompanySettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.EInvoicingCertificateEncrypted) || string.IsNullOrWhiteSpace(settings.EInvoicingPrivateKeyEncrypted))
            throw new ValidationAppException("لازم ترفع شهادة (Certificate) ومفتاح خاص (Private Key) فعليين صادرين من هيئة الزكاة والضريبة قبل إرسال أي فاتورة سعودية.");

        var certificatePem = _secretProtector.Unprotect(settings.EInvoicingCertificateEncrypted);
        var privateKeyPem = _secretProtector.Unprotect(settings.EInvoicingPrivateKeyEncrypted);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        using var certificate = X509Certificate2.CreateFromPem(certificatePem);

        var unsignedDocument = XDocument.Parse(unsignedInvoiceXml);
        var root = unsignedDocument.Root ?? throw new ValidationAppException("مستند الفاتورة غير صالح.");
        var uuid = root.Element(Cbc + "UUID")?.Value ?? throw new ValidationAppException("مستند الفاتورة ناقص عنصر UUID.");

        var invoiceHashBytes = ComputeCanonicalSha256(unsignedDocument);
        var invoiceHash = Convert.ToBase64String(invoiceHashBytes);
        var digitalSignatureBytes = ecdsa.SignData(invoiceHashBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var digitalSignature = Convert.ToBase64String(digitalSignatureBytes);

        var isSimplified = (string?)root.Element(Cbc + "InvoiceTypeCode")?.Attribute("name") is { } typeCodeName
            && typeCodeName.StartsWith("02", StringComparison.Ordinal);
        var certificateSignature = isSimplified ? ExtractCertificateSignatureBytes(certificate) : null;

        var sellerName = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party")?.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "RegistrationName")?.Value ?? settings.CompanyNameAr;
        var vatNumber = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party")?.Element(Cac + "PartyTaxScheme")?.Element(Cbc + "CompanyID")?.Value ?? settings.TaxRegistrationNumber ?? string.Empty;
        var issueDate = root.Element(Cbc + "IssueDate")?.Value ?? string.Empty;
        var issueTime = root.Element(Cbc + "IssueTime")?.Value ?? string.Empty;
        var timestamp = $"{issueDate}T{issueTime}" + (issueTime.Contains('Z') ? string.Empty : "Z");
        var invoiceTotal = decimal.Parse(root.Element(Cac + "LegalMonetaryTotal")?.Element(Cbc + "TaxInclusiveAmount")?.Value ?? "0", CultureInfo.InvariantCulture);
        var vatTotal = decimal.Parse(root.Element(Cac + "TaxTotal")?.Element(Cbc + "TaxAmount")?.Value ?? "0", CultureInfo.InvariantCulture);
        var publicKeyDer = ecdsa.ExportSubjectPublicKeyInfo();

        var qrCode = ZatcaQrCodeBuilder.Build(sellerName, vatNumber, timestamp, invoiceTotal, vatTotal, invoiceHash, digitalSignature, publicKeyDer, certificateSignature);

        var signingTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var certDigest = QuirkyHexBase64Sha256(certificatePem);
        var issuerName = FormatIssuerName(certificate);
        var serialNumber = SerialNumberDecimal(certificate);

        var signedPropertiesXml = BuildSignedPropertiesXml(signingTime, certDigest, issuerName, serialNumber);
        var signedPropertiesDigest = QuirkyHexBase64Sha256(signedPropertiesXml);

        var ublExtensions = BuildUblExtensions(invoiceHash, digitalSignature, signedPropertiesDigest, signingTime, certDigest, issuerName, serialNumber, certificate);

        // Insert ext:UBLExtensions as the very first child, then the QR reference + cac:Signature marker
        // right before AccountingSupplierParty — matching the node positions the reference implementation uses,
        // which is also where this signer's own hash computation assumed those nodes were absent from.
        root.AddFirst(ublExtensions);

        var qrNode = new XElement(Cac + "AdditionalDocumentReference",
            new XElement(Cbc + "ID", "QR"),
            new XElement(Cac + "Attachment",
                new XElement(Cbc + "EmbeddedDocumentBinaryObject", new XAttribute("mimeCode", "text/plain"), qrCode)));
        var signatureMarker = new XElement(Cac + "Signature",
            new XElement(Cbc + "ID", "urn:oasis:names:specification:ubl:signature:Invoice"),
            new XElement(Cbc + "SignatureMethod", "urn:oasis:names:specification:ubl:dsig:enveloped:xades"));

        var supplierParty = root.Element(Cac + "AccountingSupplierParty");
        if (supplierParty is not null)
            supplierParty.AddBeforeSelf(qrNode, signatureMarker);
        else
            root.Add(qrNode, signatureMarker);

        var signedXml = unsignedDocument.ToString(SaveOptions.DisableFormatting);
        return Task.FromResult(new ZatcaSigningResult(signedXml, invoiceHash, uuid));
    }

    private static byte[] ComputeCanonicalSha256(XDocument document)
    {
        using var reader = document.CreateReader();
        var xmlDocument = new XmlDocument { PreserveWhitespace = true };
        xmlDocument.Load(reader);

        var transform = new XmlDsigC14NTransform(includeComments: false);
        transform.LoadInput(xmlDocument);
        using var outputStream = (Stream)transform.GetOutput(typeof(Stream));
        using var buffer = new MemoryStream();
        outputStream.CopyTo(buffer);
        return SHA256.HashData(buffer.ToArray());
    }

    /// <summary>Replicates the reference implementation's base64(hex(sha256(x))) pattern — see caveat #1 in
    /// the class doc comment. NOT the same as a standard base64(raw sha256 bytes) digest.</summary>
    private static string QuirkyHexBase64Sha256(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return Convert.ToBase64String(Encoding.ASCII.GetBytes(hex));
    }

    private static string FormatIssuerName(X509Certificate2 certificate)
    {
        var parts = certificate.IssuerName.Format(false)
            .Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Array.Reverse(parts);
        return string.Join(", ", parts);
    }

    private static string SerialNumberDecimal(X509Certificate2 certificate)
    {
        var serialBytes = certificate.GetSerialNumber(); // little-endian per .NET convention
        var value = new BigInteger(serialBytes);
        if (value.Sign < 0)
        {
            var padded = new byte[serialBytes.Length + 1];
            Array.Copy(serialBytes, padded, serialBytes.Length);
            value = new BigInteger(padded);
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static byte[]? ExtractCertificateSignatureBytes(X509Certificate2 certificate)
    {
        try
        {
            var reader = new AsnReader(certificate.RawData, AsnEncodingRules.DER);
            var certificateSequence = reader.ReadSequence();
            certificateSequence.ReadEncodedValue(); // tbsCertificate — skip
            certificateSequence.ReadEncodedValue(); // signatureAlgorithm — skip
            return certificateSequence.ReadBitString(out _);
        }
        catch (AsnContentException)
        {
            // Best-effort only — tag 9 is an addition on top of the core cryptographic stamp, not required
            // for the ECDSA signature itself to be valid, so a parsing failure here shouldn't block signing.
            return null;
        }
    }

    /// <summary>Exact literal template (including whitespace) whose UTF-8 bytes are hashed for the
    /// SignedProperties ds:Reference digest — the reference source is explicit that altering this spacing
    /// changes the digest, so it's reproduced character-for-character rather than rebuilt from an XElement.</summary>
    private static string BuildSignedPropertiesXml(string signingTime, string certDigest, string issuerName, string serialNumber)
    {
        var sb = new StringBuilder();
        sb.Append("<xades:SignedProperties xmlns:xades=\"http://uri.etsi.org/01903/v1.3.2#\" Id=\"xadesSignedProperties\">\n");
        sb.Append("                                <xades:SignedSignatureProperties>\n");
        sb.Append($"                                    <xades:SigningTime>{signingTime}</xades:SigningTime>\n");
        sb.Append("                                    <xades:SigningCertificate>\n");
        sb.Append("                                        <xades:Cert>\n");
        sb.Append("                                            <xades:CertDigest>\n");
        sb.Append("                                                <ds:DigestMethod xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"/>\n");
        sb.Append($"                                                <ds:DigestValue xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">{certDigest}</ds:DigestValue>\n");
        sb.Append("                                            </xades:CertDigest>\n");
        sb.Append("                                            <xades:IssuerSerial>\n");
        sb.Append($"                                                <ds:X509IssuerName xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">{issuerName}</ds:X509IssuerName>\n");
        sb.Append($"                                                <ds:X509SerialNumber xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">{serialNumber}</ds:X509SerialNumber>\n");
        sb.Append("                                            </xades:IssuerSerial>\n");
        sb.Append("                                        </xades:Cert>\n");
        sb.Append("                                    </xades:SigningCertificate>\n");
        sb.Append("                                </xades:SignedSignatureProperties>\n");
        sb.Append("                            </xades:SignedProperties>");
        return sb.ToString();
    }

    private static XElement BuildUblExtensions(
        string invoiceDigest, string signatureValue, string signedPropertiesDigest,
        string signingTime, string certDigest, string issuerName, string serialNumber, X509Certificate2 certificate)
    {
        var signedProperties = new XElement(Xades + "SignedProperties",
            new XAttribute("Id", "xadesSignedProperties"),
            new XElement(Xades + "SignedSignatureProperties",
                new XElement(Xades + "SigningTime", signingTime),
                new XElement(Xades + "SigningCertificate",
                    new XElement(Xades + "Cert",
                        new XElement(Xades + "CertDigest",
                            new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")),
                            new XElement(Ds + "DigestValue", certDigest)),
                        new XElement(Xades + "IssuerSerial",
                            new XElement(Ds + "X509IssuerName", issuerName),
                            new XElement(Ds + "X509SerialNumber", serialNumber))))));

        var qualifyingProperties = new XElement(Xades + "QualifyingProperties",
            new XAttribute("Target", "signature"),
            signedProperties);

        var signedInfo = new XElement(Ds + "SignedInfo",
            new XElement(Ds + "CanonicalizationMethod", new XAttribute("Algorithm", "http://www.w3.org/2006/12/xml-c14n11")),
            new XElement(Ds + "SignatureMethod", new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256")),
            new XElement(Ds + "Reference",
                new XAttribute("Id", "invoiceSignedData"),
                new XAttribute("URI", string.Empty),
                new XElement(Ds + "Transforms",
                    new XElement(Ds + "Transform",
                        new XAttribute("Algorithm", "http://www.w3.org/TR/1999/REC-xpath-19991116"),
                        new XElement(Ds + "XPath", "not(//ancestor-or-self::ext:UBLExtensions)")),
                    new XElement(Ds + "Transform",
                        new XAttribute("Algorithm", "http://www.w3.org/TR/1999/REC-xpath-19991116"),
                        new XElement(Ds + "XPath", "not(//ancestor-or-self::cac:Signature)")),
                    new XElement(Ds + "Transform",
                        new XAttribute("Algorithm", "http://www.w3.org/TR/1999/REC-xpath-19991116"),
                        new XElement(Ds + "XPath", "not(//ancestor-or-self::cac:AdditionalDocumentReference[cbc:ID='QR'])")),
                    new XElement(Ds + "Transform", new XAttribute("Algorithm", "http://www.w3.org/2006/12/xml-c14n11"))),
                new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")),
                new XElement(Ds + "DigestValue", invoiceDigest)),
            new XElement(Ds + "Reference",
                new XAttribute("Type", "http://www.w3.org/2000/09/xmldsig#SignatureProperties"),
                new XAttribute("URI", "#xadesSignedProperties"),
                new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")),
                new XElement(Ds + "DigestValue", signedPropertiesDigest)));

        var dsSignature = new XElement(Ds + "Signature",
            new XAttribute("Id", "signature"),
            signedInfo,
            new XElement(Ds + "SignatureValue", signatureValue),
            new XElement(Ds + "KeyInfo",
                new XElement(Ds + "X509Data",
                    new XElement(Ds + "X509Certificate", Convert.ToBase64String(certificate.RawData)))),
            new XElement(Ds + "Object", qualifyingProperties));

        var signatureInformation = new XElement(Sac + "SignatureInformation",
            new XElement(Cbc + "ID", "urn:oasis:names:specification:ubl:signature:1"),
            new XElement(Sbc + "ReferencedSignatureID", "urn:oasis:names:specification:ubl:signature:Invoice"),
            dsSignature);

        var documentSignatures = new XElement(Sig + "UBLDocumentSignatures",
            new XAttribute(XNamespace.Xmlns + "sac", Sac),
            new XAttribute(XNamespace.Xmlns + "sbc", Sbc),
            signatureInformation);

        var extension = new XElement(Ext + "UBLExtension",
            new XElement(Ext + "ExtensionURI", "urn:oasis:names:specification:ubl:dsig:enveloped:xades"),
            new XElement(Ext + "ExtensionContent", documentSignatures));

        return new XElement(Ext + "UBLExtensions", extension);
    }
}
