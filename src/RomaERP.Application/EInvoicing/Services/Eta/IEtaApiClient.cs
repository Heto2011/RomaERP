using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Eta;

public record EtaSubmissionResponse(bool Accepted, string? Uuid, string? ErrorMessage);

/// <summary>Talks to the real ETA REST API (OAuth login, then POST the signed document to the invoice
/// submission endpoint at https://sdk.invoicing.eta.gov.eg/). Not implemented against the live endpoint yet —
/// this session had no real ETA credentials and the ETA domains are unreachable from this sandbox, so wire
/// the actual HTTP calls and verify them against a real sandbox account before switching off the mock.</summary>
public interface IEtaApiClient
{
    Task<EtaSubmissionResponse> SubmitSignedDocumentAsync(string signedDocument, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in that always accepts, returning a fake UUID. Never use against a real
/// (non-mock) ETA environment.</summary>
public class MockEtaApiClient : IEtaApiClient
{
    public Task<EtaSubmissionResponse> SubmitSignedDocumentAsync(string signedDocument, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new EtaSubmissionResponse(true, $"MOCK-ETA-{Guid.NewGuid():N}", null));
}
