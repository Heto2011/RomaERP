using RomaERP.Domain.EInvoicing;

namespace RomaERP.Application.EInvoicing.DTOs;

public class EInvoicingSettingsDto
{
    public EInvoicingProvider Provider { get; set; }
    public EInvoicingEnvironment Environment { get; set; }
    public bool HasClientCredentials { get; set; }
    public bool HasCertificate { get; set; }
}

public class UpdateEInvoicingSettingsDto
{
    public EInvoicingProvider Provider { get; set; }
    public EInvoicingEnvironment Environment { get; set; }
    /// <summary>Leave null to keep the existing stored client ID unchanged.</summary>
    public string? ClientId { get; set; }
    /// <summary>Plain text — encrypted before storage. Leave null to keep the existing secret unchanged.</summary>
    public string? ClientSecret { get; set; }
    /// <summary>ZATCA only: PEM certificate issued by ZATCA. Leave null to keep unchanged.</summary>
    public string? Certificate { get; set; }
    /// <summary>ZATCA only: signing private key. Leave null to keep unchanged.</summary>
    public string? PrivateKey { get; set; }
}

public class EInvoiceStatusDto
{
    public Guid SalesInvoiceId { get; set; }
    public EInvoiceStatus Status { get; set; }
    public string? ExternalUuid { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EInvoiceNoteStatusDto
{
    public Guid SalesNoteId { get; set; }
    public EInvoiceStatus Status { get; set; }
    public string? ExternalUuid { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
