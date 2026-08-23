using RomaERP.Domain.Common;
using RomaERP.Domain.EInvoicing;

namespace RomaERP.Domain.Tenancy;

/// <summary>Single-row settings living inside each tenant's own database (company profile, country, tax).</summary>
public class CompanySettings : AuditableEntity
{
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Country Country { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public decimal VatRate { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";

    // ----- E-invoicing (government tax authority integration) -----
    public EInvoicingProvider EInvoicingProvider { get; set; } = EInvoicingProvider.None;
    public EInvoicingEnvironment EInvoicingEnvironment { get; set; } = EInvoicingEnvironment.Sandbox;
    public string? EInvoicingClientId { get; set; }
    /// <summary>Encrypted at rest via ISecretProtector — never stored or returned in plain text.</summary>
    public string? EInvoicingClientSecretEncrypted { get; set; }
    /// <summary>ZATCA only: the CSID certificate (PEM) issued by ZATCA during onboarding. Encrypted at rest.</summary>
    public string? EInvoicingCertificateEncrypted { get; set; }
    /// <summary>ZATCA only: the secp256k1 private key used to sign invoices. Encrypted at rest.</summary>
    public string? EInvoicingPrivateKeyEncrypted { get; set; }
    /// <summary>ZATCA only: base64 hash of the last successfully submitted invoice, chained into the next invoice's PIH.</summary>
    public string? EInvoicingLastInvoiceHash { get; set; }
    /// <summary>ZATCA only: running count of invoices submitted, used as the next invoice's ICV (Invoice Counter Value).</summary>
    public int EInvoicingSubmittedCount { get; set; }

    // ----- ZATCA CSID onboarding wizard (Create CSR → Compliance CSID → Compliance Checks → Production CSID) -----
    public ZatcaOnboardingStage EInvoicingZatcaOnboardingStage { get; set; } = ZatcaOnboardingStage.NotStarted;
    /// <summary>15-digit VAT/organization identifier for the CSR — starts and ends with '3'. Distinct from the
    /// general TaxRegistrationNumber field since ZATCA validates this exact format for the certificate subject.</summary>
    public string? EInvoicingZatcaOrganizationIdentifier { get; set; }
    public string? EInvoicingZatcaSolutionName { get; set; }
    public string? EInvoicingZatcaModel { get; set; }
    public string? EInvoicingZatcaDeviceSerialNumber { get; set; }
    public string? EInvoicingZatcaOrganizationUnitName { get; set; }
    public string? EInvoicingZatcaAddress { get; set; }
    public string? EInvoicingZatcaBusinessCategory { get; set; }
    /// <summary>4-digit bitmask, e.g. "1100" for Standard+Simplified. Defaults to both.</summary>
    public string EInvoicingZatcaInvoiceType { get; set; } = "1100";
    /// <summary>The CSR itself — not confidential (it's sent to ZATCA and contains only the public key), so
    /// stored in plain text unlike the certificate/private key/secret fields above.</summary>
    public string? EInvoicingZatcaCsrPem { get; set; }
    public string? EInvoicingZatcaComplianceRequestId { get; set; }
}
