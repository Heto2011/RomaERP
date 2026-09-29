using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.Services.Eta;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.EInvoicing.Eta;

/// <summary>
/// Real HTTP client for ETA's (Egyptian Tax Authority) e-invoicing API — OAuth2 client-credentials login
/// against ETA's identity server, then batch document submission against the invoicing API. Endpoint paths,
/// the client-credentials grant shape, and the submission request/response field names (documents /
/// acceptedDocuments / rejectedDocuments / uuid) follow ETA's publicly published e-invoicing SDK documentation.
///
/// UNVERIFIED — same caveat as ZatcaHttpApiClient: this session has no network access to eta.gov.eg (blocked in
/// this sandbox) and no real ETA client credentials, so none of these calls have actually been exercised
/// against a real ETA environment. Confirm against ETA's real preprod environment (a real Client ID/Secret from
/// the taxpayer's own ETA e-invoicing portal registration) before relying on this in production.
/// </summary>
public class EtaHttpApiClient : IEtaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ISecretProtector _secretProtector;

    public EtaHttpApiClient(HttpClient httpClient, ISecretProtector secretProtector)
    {
        _httpClient = httpClient;
        _secretProtector = secretProtector;
    }

    private static string GetIdentityUrl(EInvoicingEnvironment environment) => environment switch
    {
        EInvoicingEnvironment.Production => "https://id.eta.gov.eg/connect/token",
        _ => "https://id.preprod.eta.gov.eg/connect/token",
    };

    private static string GetApiBaseUrl(EInvoicingEnvironment environment) => environment switch
    {
        EInvoicingEnvironment.Production => "https://api.invoicing.eta.gov.eg",
        _ => "https://api.preprod.invoicing.eta.gov.eg",
    };

    public async Task<EtaSubmissionResponse> SubmitSignedDocumentAsync(string signedDocument, CompanySettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.EInvoicingClientId) || string.IsNullOrWhiteSpace(settings.EInvoicingClientSecretEncrypted))
            return new EtaSubmissionResponse(false, null, "لسه معملتش تسجيل الـ Client ID والـ Client Secret بتوع مصلحة الضرائب المصرية (ETA) في إعدادات الفوترة الإلكترونية.");

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(settings, ct);
        }
        catch (EtaApiException ex)
        {
            return new EtaSubmissionResponse(false, null, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return new EtaSubmissionResponse(false, null, $"تعذر تسجيل الدخول لمنظومة مصلحة الضرائب (ETA): {ex.Message}");
        }

        JsonElement document;
        try
        {
            document = JsonSerializer.Deserialize<JsonElement>(signedDocument);
        }
        catch (JsonException ex)
        {
            return new EtaSubmissionResponse(false, null, $"المستند الموقّع مش JSON صالح: {ex.Message}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, GetApiBaseUrl(settings.EInvoicingEnvironment) + "/api/v1/documentsubmissions")
        {
            Content = JsonContent.Create(new { documents = new[] { document } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return new EtaSubmissionResponse(false, null, $"تعذر الاتصال بمنظومة مصلحة الضرائب (ETA): {ex.Message}");
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        if ((int)response.StatusCode >= 300)
            return new EtaSubmissionResponse(false, null, $"خطأ من منظومة مصلحة الضرائب (ETA) (HTTP {(int)response.StatusCode}): {content}");

        JsonElement result;
        try
        {
            result = JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            return new EtaSubmissionResponse(false, null, $"استجابة غير متوقعة من مصلحة الضرائب (ETA): {content}");
        }

        if (result.TryGetProperty("rejectedDocuments", out var rejected) && rejected.ValueKind == JsonValueKind.Array && rejected.GetArrayLength() > 0)
        {
            var firstError = rejected[0].TryGetProperty("error", out var err) ? err.ToString() : content;
            return new EtaSubmissionResponse(false, null, $"رفضت مصلحة الضرائب الفاتورة: {firstError}");
        }

        var uuid = result.TryGetProperty("acceptedDocuments", out var accepted)
            && accepted.ValueKind == JsonValueKind.Array
            && accepted.GetArrayLength() > 0
            && accepted[0].TryGetProperty("uuid", out var u)
                ? u.GetString()
                : null;

        return new EtaSubmissionResponse(true, uuid, null);
    }

    private async Task<string> GetAccessTokenAsync(CompanySettings settings, CancellationToken ct)
    {
        var clientSecret = _secretProtector.Unprotect(settings.EInvoicingClientSecretEncrypted!);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = settings.EInvoicingClientId!,
            ["client_secret"] = clientSecret,
            ["scope"] = "InvoicingAPI",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GetIdentityUrl(settings.EInvoicingEnvironment))
        {
            Content = new FormUrlEncodedContent(form),
        };

        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new EtaApiException($"فشل تسجيل الدخول لمنظومة مصلحة الضرائب (ETA) (HTTP {(int)response.StatusCode}): {content}");

        JsonElement json;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            throw new EtaApiException($"استجابة غير متوقعة من منظومة مصلحة الضرائب (ETA) أثناء تسجيل الدخول: {content}");
        }

        var token = json.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(token))
            throw new EtaApiException("استجابة غير متوقعة من منظومة مصلحة الضرائب (ETA) أثناء تسجيل الدخول — access_token مش موجود.");

        return token;
    }
}

public class EtaApiException : Exception
{
    public EtaApiException(string message) : base(message) { }
}
