using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

public record ZatcaSubmissionResponse(bool Success, string? ErrorMessage);

/// <summary>CertificatePem: PEM-encoded X.509 certificate issued by ZATCA (compliance or production, depending
/// on which endpoint returned it). Secret: the opaque credential paired with the certificate for Basic auth on
/// subsequent calls. RequestId: needed to exchange a Compliance CSID for a Production CSID.</summary>
public record ZatcaCsidResult(string CertificatePem, string Secret, string RequestId);

public record ZatcaComplianceCheckResult(bool Success, string? Status, string? ErrorMessage);

/// <summary>Talks to the real ZATCA Fatoora API — both the CSID onboarding lifecycle (Compliance CSID →
/// compliance checks → Production CSID) and invoice submission (Reporting for Simplified/B2C, Clearance for
/// Standard/B2B), against one of ZATCA's three environment tiers (sandbox / simulation / production). See
/// RomaERP.Infrastructure.EInvoicing.Zatca.ZatcaHttpApiClient for the real implementation and its caveats.</summary>
public interface IZatcaApiClient
{
    Task<ZatcaCsidResult> RequestComplianceCertificateAsync(string csrPem, string otp, CompanySettings settings, CancellationToken ct = default);
    Task<ZatcaCsidResult> RequestProductionCertificateAsync(string complianceCertificatePem, string complianceSecret, string complianceRequestId, CompanySettings settings, CancellationToken ct = default);
    Task<ZatcaComplianceCheckResult> ValidateComplianceInvoiceAsync(string complianceCertificatePem, string complianceSecret, string signedInvoiceXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default);
    Task<ZatcaSubmissionResponse> ReportSimplifiedInvoiceAsync(string signedXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default);
    Task<ZatcaSubmissionResponse> ClearStandardInvoiceAsync(string signedXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in that always succeeds without any network call. Used only where no real
/// ZATCA CSID is configured (e.g. unit tests exercising submission orchestration rather than the real API
/// integration). Never registered against a real (non-mock) ZATCA environment — see
/// RomaERP.Infrastructure.DependencyInjection for the real ZatcaHttpApiClient registration.</summary>
public class MockZatcaApiClient : IZatcaApiClient
{
    public Task<ZatcaCsidResult> RequestComplianceCertificateAsync(string csrPem, string otp, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaCsidResult("-----BEGIN CERTIFICATE-----\nMOCK\n-----END CERTIFICATE-----", "mock-secret", Guid.NewGuid().ToString()));

    public Task<ZatcaCsidResult> RequestProductionCertificateAsync(string complianceCertificatePem, string complianceSecret, string complianceRequestId, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaCsidResult("-----BEGIN CERTIFICATE-----\nMOCK\n-----END CERTIFICATE-----", "mock-secret", Guid.NewGuid().ToString()));

    public Task<ZatcaComplianceCheckResult> ValidateComplianceInvoiceAsync(string complianceCertificatePem, string complianceSecret, string signedInvoiceXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaComplianceCheckResult(true, "PASS", null));

    public Task<ZatcaSubmissionResponse> ReportSimplifiedInvoiceAsync(string signedXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaSubmissionResponse(true, null));

    public Task<ZatcaSubmissionResponse> ClearStandardInvoiceAsync(string signedXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaSubmissionResponse(true, null));
}
