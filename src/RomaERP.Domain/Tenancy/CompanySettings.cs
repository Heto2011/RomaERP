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
}
