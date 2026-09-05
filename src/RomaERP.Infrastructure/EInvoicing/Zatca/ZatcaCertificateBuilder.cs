using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.EInvoicing;

namespace RomaERP.Infrastructure.EInvoicing.Zatca;

public record ZatcaCsrResult(string CsrPem, string PrivateKeyPem);

public class ZatcaCsrOptions
{
    /// <summary>15 digits, must start and end with '3'.</summary>
    public required string OrganizationIdentifier { get; init; }
    public required string SolutionName { get; init; }
    public required string Model { get; init; }
    public required string DeviceSerialNumber { get; init; }
    public required string CommonName { get; init; }
    public string Country { get; init; } = "SA";
    public required string OrganizationName { get; init; }
    public required string OrganizationalUnitName { get; init; }
    public required string Address { get; init; }
    /// <summary>4-digit bitmask, e.g. "1100" for Standard+Simplified.</summary>
    public string InvoiceType { get; init; } = "1100";
    public required string BusinessCategory { get; init; }
    public required EInvoicingEnvironment Environment { get; init; }
}

/// <summary>
/// Generates a ZATCA-compliant CSR (Certificate Signing Request) and its matching secp256k1 private key —
/// the first step of the CSID onboarding wizard ("Create CSR" in Odoo's l10n_sa_edi terminology).
///
/// Ported field-for-field from the openly available "Saleh7/php-zatca-xml" reference implementation's
/// CertificateBuilder class, which documents itself as aligned with ZATCA's official Java SDK (R3.4.8) CSR
/// generation — same curve, same DN attribute order (C → OU → O → CN), same custom OID extension for the
/// certificate template, and the same "dirName" Subject Alternative Name carrying the serial number,
/// organization identifier, invoice type, address, and business category. RomaERP has no official ZATCA
/// documentation of its own and no network access to verify this independently, so this is only as accurate
/// as that reference — it has NOT been submitted to ZATCA's real /compliance endpoint and confirmed accepted.
/// </summary>
public static class ZatcaCertificateBuilder
{
    private static readonly Regex ForbiddenChars = new(@"[!@#$%&*_<=]", RegexOptions.Compiled);
    private static readonly Regex OrganizationIdentifierPattern = new(@"^3\d{13}3$", RegexOptions.Compiled);
    private static readonly Regex InvoiceTypePattern = new(@"^[01]{4}$", RegexOptions.Compiled);

    public static ZatcaCsrResult Generate(ZatcaCsrOptions options)
    {
        Validate(options);

        using var ecdsa = ECDsa.Create(ECCurve.CreateFromOid(new Oid("1.3.132.0.10", "secp256k1")));

        var nameBuilder = new X500DistinguishedNameBuilder();
        nameBuilder.AddCountryOrRegion(options.Country.ToUpperInvariant());
        nameBuilder.AddOrganizationalUnitName(options.OrganizationalUnitName);
        nameBuilder.AddOrganizationName(options.OrganizationName);
        nameBuilder.AddCommonName(options.CommonName);
        var subject = nameBuilder.Build();

        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(BuildCertificateTemplateExtension(options.Environment));
        request.CertificateExtensions.Add(BuildDirectoryNameSanExtension(options));

        var csrDer = request.CreateSigningRequest();
        var csrPem = PemEncoding.WriteString("CERTIFICATE REQUEST", csrDer);
        var privateKeyPem = ecdsa.ExportECPrivateKeyPem();

        return new ZatcaCsrResult(csrPem, privateKeyPem);
    }

    private static void Validate(ZatcaCsrOptions options)
    {
        if (!OrganizationIdentifierPattern.IsMatch(options.OrganizationIdentifier))
            throw new ValidationAppException("الرقم التعريفي للمنشأة (Organization Identifier) لازم يكون 15 رقم، ويبدأ وينتهي بالرقم 3.");

        foreach (var (value, label) in new[]
                 {
                     (options.CommonName, "الاسم الشائع (Common Name)"),
                     (options.OrganizationName, "اسم المنشأة"),
                     (options.OrganizationalUnitName, "اسم الفرع/الوحدة"),
                     (options.Address, "العنوان"),
                     (options.BusinessCategory, "النشاط التجاري"),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ValidationAppException($"{label} مطلوب.");
            if (ForbiddenChars.IsMatch(value))
                throw new ValidationAppException($"{label} فيه رموز غير مسموحة (!@#$%&*_<=).");
        }

        if (string.IsNullOrWhiteSpace(options.SolutionName) || string.IsNullOrWhiteSpace(options.Model) || string.IsNullOrWhiteSpace(options.DeviceSerialNumber))
            throw new ValidationAppException("اسم الحل ونوع الجهاز والرقم التسلسلي مطلوبين.");

        if (!InvoiceTypePattern.IsMatch(options.InvoiceType))
            throw new ValidationAppException("نوع الفاتورة لازم يكون 4 أرقام، كل رقم 0 أو 1 (مثال: 1100).");

        if (options.Country.Length is < 2 or > 3)
            throw new ValidationAppException("كود الدولة لازم يكون 2 أو 3 حروف.");

        // ZATCA's VAT-group rule: when the 11th digit (0-based index 10) of the organization identifier is
        // '1', the organizational unit name must be the 10-digit TIN of the specific group member.
        if (options.OrganizationIdentifier.Length > 10 && options.OrganizationIdentifier[10] == '1'
            && options.OrganizationalUnitName.Length != 10)
        {
            throw new ValidationAppException("لأن الرقم التعريفي ده لمجموعة ضريبية (VAT Group)، اسم الفرع/الوحدة لازم يكون الرقم الضريبي (TIN) المكوّن من 10 أرقام لعضو المجموعة صاحب الجهاز ده.");
        }
    }

    private static X509Extension BuildCertificateTemplateExtension(EInvoicingEnvironment environment)
    {
        var templateName = environment switch
        {
            EInvoicingEnvironment.Production => "ZATCA-Code-Signing",
            EInvoicingEnvironment.Sandbox => "TSTZATCA-Code-Signing",
            EInvoicingEnvironment.Simulation => "PREZATCA-Code-Signing",
            _ => throw new ArgumentOutOfRangeException(nameof(environment)),
        };

        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteCharacterString(UniversalTagNumber.UTF8String, templateName);
        // OID 1.3.6.1.4.1.311.20.2 (Microsoft Certificate Template Name) — ZATCA's SDK repurposes it to
        // signal which environment tier this CSR/certificate belongs to.
        return new X509Extension(new Oid("1.3.6.1.4.1.311.20.2"), writer.Encode(), critical: false);
    }

    /// <summary>Builds subjectAltName = dirName:{SN, UID, title, registeredAddress, businessCategory} — a
    /// directory-name SAN entry that .NET's SubjectAlternativeNameBuilder doesn't support natively, so it's
    /// hand-built via ASN.1: GeneralNames ::= SEQUENCE OF GeneralName, with directoryName as the implicitly
    /// tagged [4] choice wrapping a Name (RDNSequence of single-attribute SETs).</summary>
    private static X509Extension BuildDirectoryNameSanExtension(ZatcaCsrOptions options)
    {
        var serialNumberValue = $"1-{options.SolutionName}|2-{options.Model}|3-{options.DeviceSerialNumber}";
        if (serialNumberValue.Contains('='))
            throw new ValidationAppException("اسم الحل أو نوع الجهاز أو الرقم التسلسلي فيه علامة (=) غير مسموحة.");

        var attributes = new (string Oid, string Value)[]
        {
            ("2.5.4.5", serialNumberValue), // serialNumber (SN)
            ("0.9.2342.19200300.100.1.1", options.OrganizationIdentifier), // userId (UID)
            ("2.5.4.12", options.InvoiceType), // title
            ("2.5.4.26", options.Address), // registeredAddress
            ("2.5.4.15", options.BusinessCategory), // businessCategory
        };

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence()) // GeneralNames
        {
            var directoryNameTag = new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true);
            using (writer.PushSequence(directoryNameTag)) // Name (RDNSequence), IMPLICIT [4] instead of SEQUENCE
            {
                foreach (var (oid, value) in attributes)
                {
                    using (writer.PushSetOf()) // RelativeDistinguishedName
                    using (writer.PushSequence()) // AttributeTypeAndValue
                    {
                        writer.WriteObjectIdentifier(oid);
                        writer.WriteCharacterString(UniversalTagNumber.UTF8String, value);
                    }
                }
            }
        }

        return new X509Extension(new Oid("2.5.29.17"), writer.Encode(), critical: false);
    }
}
