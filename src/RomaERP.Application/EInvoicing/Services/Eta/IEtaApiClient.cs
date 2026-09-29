using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.EInvoicing.Services.Eta;

public record EtaSubmissionResponse(bool Accepted, string? Uuid, string? ErrorMessage);

/// <summary>Talks to the real ETA REST API — OAuth2 client-credentials login against ETA's identity server,
/// then POST the signed document to the invoice submission endpoint. See
/// RomaERP.Infrastructure.EInvoicing.Eta.EtaHttpApiClient for the real implementation and its caveats (this
/// session had no real ETA credentials and the ETA domains are unreachable from this sandbox, so the request
/// shape follows ETA's published SDK docs but hasn't been exercised against a live endpoint).</summary>
public interface IEtaApiClient
{
    Task<EtaSubmissionResponse> SubmitSignedDocumentAsync(string signedDocument, CompanySettings settings, CancellationToken ct = default);
}

/// <summary>Development/demo stand-in that always accepts, returning a fake UUID. Only used by unit tests
/// exercising submission orchestration — the real EtaHttpApiClient is what's registered in production (see
/// RomaERP.Infrastructure.DependencyInjection).</summary>
public class MockEtaApiClient : IEtaApiClient
{
    public Task<EtaSubmissionResponse> SubmitSignedDocumentAsync(string signedDocument, CompanySettings settings, CancellationToken ct = default)
        => Task.FromResult(new EtaSubmissionResponse(true, $"MOCK-ETA-{Guid.NewGuid():N}", null));
}
