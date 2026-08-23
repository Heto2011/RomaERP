using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Zatca;

public record ZatcaSubmissionResponse(bool Success, string? ErrorMessage);

/// <summary>Talks to the real ZATCA Fatoora API: POST /invoices/reporting/single for Simplified (B2C)
/// invoices, POST /invoices/clearance/single for Standard (B2B) ones, against one of ZATCA's three
/// environment tiers (sandbox / simulation / production). Not implemented against the live endpoint yet —
/// this session had no real ZATCA CSID/credentials and zatca.gov.sa is unreachable from this sandbox, so wire
/// the actual HTTP calls and verify them against a real sandbox account before switching off the mock.</summary>
public interface IZatcaApiClient
{
    Task<ZatcaSubmissionResponse> ReportSimplifiedInvoiceAsync(string signedXml, CompanySettings settings, CancellationToken ct = default);
    Task<ZatcaSubmissionResponse> ClearStandardInvoiceAsync(string signedXml, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in that always succeeds. Never use against a real (non-mock) ZATCA
/// environment.</summary>
public class MockZatcaApiClient : IZatcaApiClient
{
    public Task<ZatcaSubmissionResponse> ReportSimplifiedInvoiceAsync(string signedXml, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaSubmissionResponse(true, null));

    public Task<ZatcaSubmissionResponse> ClearStandardInvoiceAsync(string signedXml, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new ZatcaSubmissionResponse(true, null));
}
