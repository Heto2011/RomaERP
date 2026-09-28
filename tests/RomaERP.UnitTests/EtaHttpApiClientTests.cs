using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.EInvoicing.Eta;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Verifies EtaHttpApiClient builds requests matching ETA's publicly documented OAuth2
/// client-credentials + document-submission shape — since there's no network access to ETA's real servers in
/// this environment to verify against directly. A request shape matching the docs is necessary but not
/// sufficient for ETA to actually accept it.</summary>
public class EtaHttpApiClientTests
{
    private class RecordingHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        public readonly List<string?> Bodies = new();
        public Func<HttpRequestMessage, (HttpStatusCode Status, string Body)>? Respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = Respond?.Invoke(request) ?? (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (EtaHttpApiClient Client, RecordingHandler Handler) BuildClient(PlainTextSecretProtector protector)
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        return (new EtaHttpApiClient(httpClient, protector), handler);
    }

    private static CompanySettings EgyptSettings(PlainTextSecretProtector protector) => new()
    {
        CompanyNameAr = "شركة تجريبية",
        CompanyNameEn = "Test Co",
        Country = Country.Egypt,
        VatRate = 0.14m,
        DefaultCurrency = "EGP",
        EInvoicingEnvironment = EInvoicingEnvironment.Sandbox,
        EInvoicingClientId = "client-123",
        EInvoicingClientSecretEncrypted = protector.Protect("client-secret"),
    };

    [Fact]
    public async Task SubmitSignedDocumentAsync_WithoutClientCredentials_ReturnsFailureWithoutCallingNetwork()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = EgyptSettings(protector);
        settings.EInvoicingClientId = null;

        var result = await client.SubmitSignedDocumentAsync("{}", settings);

        Assert.False(result.Accepted);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SubmitSignedDocumentAsync_LogsInThenSubmitsWithBearerToken()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = EgyptSettings(protector);

        handler.Respond = req =>
        {
            if (req.RequestUri!.ToString().Contains("connect/token"))
                return (HttpStatusCode.OK, JsonSerializer.Serialize(new { access_token = "TOKEN-ABC", expires_in = 3600, token_type = "Bearer" }));
            return (HttpStatusCode.OK, JsonSerializer.Serialize(new { submissionId = "sub-1", acceptedDocuments = new[] { new { uuid = "UUID-1" } }, rejectedDocuments = Array.Empty<object>() }));
        };

        var result = await client.SubmitSignedDocumentAsync("""{"documentType":"I"}""", settings);

        Assert.True(result.Accepted);
        Assert.Equal("UUID-1", result.Uuid);
        Assert.Equal(2, handler.Requests.Count);

        var tokenRequest = handler.Requests[0];
        Assert.Equal("https://id.preprod.eta.gov.eg/connect/token", tokenRequest.RequestUri!.ToString());
        var tokenBody = handler.Bodies[0];
        Assert.Contains("client_id=client-123", tokenBody);
        Assert.Contains("client_secret=client-secret", tokenBody);
        Assert.Contains("grant_type=client_credentials", tokenBody);

        var submitRequest = handler.Requests[1];
        Assert.Equal("https://api.preprod.invoicing.eta.gov.eg/api/v1/documentsubmissions", submitRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", submitRequest.Headers.Authorization!.Scheme);
        Assert.Equal("TOKEN-ABC", submitRequest.Headers.Authorization.Parameter);
        Assert.Contains("\"documentType\":\"I\"", handler.Bodies[1]);
    }

    [Fact]
    public async Task SubmitSignedDocumentAsync_UsesProductionUrlsWhenEnvironmentIsProduction()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = EgyptSettings(protector);
        settings.EInvoicingEnvironment = EInvoicingEnvironment.Production;

        handler.Respond = req => req.RequestUri!.ToString().Contains("connect/token")
            ? (HttpStatusCode.OK, JsonSerializer.Serialize(new { access_token = "TOKEN" }))
            : (HttpStatusCode.OK, JsonSerializer.Serialize(new { acceptedDocuments = new[] { new { uuid = "U" } }, rejectedDocuments = Array.Empty<object>() }));

        await client.SubmitSignedDocumentAsync("{}", settings);

        Assert.Equal("https://id.eta.gov.eg/connect/token", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://api.invoicing.eta.gov.eg/api/v1/documentsubmissions", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task SubmitSignedDocumentAsync_WhenRejected_ReturnsFailureWithEtaErrorDetail()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = EgyptSettings(protector);

        handler.Respond = req => req.RequestUri!.ToString().Contains("connect/token")
            ? (HttpStatusCode.OK, JsonSerializer.Serialize(new { access_token = "TOKEN" }))
            : (HttpStatusCode.OK, JsonSerializer.Serialize(new { acceptedDocuments = Array.Empty<object>(), rejectedDocuments = new[] { new { error = "Invalid tax number" } } }));

        var result = await client.SubmitSignedDocumentAsync("{}", settings);

        Assert.False(result.Accepted);
        Assert.Contains("Invalid tax number", result.ErrorMessage);
    }

    [Fact]
    public async Task SubmitSignedDocumentAsync_WhenLoginFails_ReturnsFailureWithoutSubmitting()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = EgyptSettings(protector);

        handler.Respond = _ => (HttpStatusCode.Unauthorized, JsonSerializer.Serialize(new { error = "invalid_client" }));

        var result = await client.SubmitSignedDocumentAsync("{}", settings);

        Assert.False(result.Accepted);
        Assert.NotNull(result.ErrorMessage);
        Assert.Single(handler.Requests); // only the token attempt, never the submission
    }
}
