using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.EInvoicing.Services.Zatca;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Infrastructure.EInvoicing.Zatca;

/// <summary>
/// Real HTTP client for ZATCA's Fatoora e-invoicing API — onboarding (CSID lifecycle) and invoice
/// submission. Endpoint paths, headers, request/response field names, and the Basic-auth encoding scheme were
/// read directly from the openly available "Saleh7/php-zatca-xml" reference implementation's ZatcaAPI class,
/// since RomaERP has no official ZATCA API documentation of its own.
///
/// UNVERIFIED: this session has no network access to zatca.gov.sa (blocked in this sandbox), so none of these
/// calls have actually been exercised against a real ZATCA environment. The request-building logic is tested
/// (see ZatcaHttpApiClientTests) against a fake HTTP handler that asserts the exact URL/headers/body shape
/// matches the reference — but that only proves internal consistency with the reference, not that ZATCA's
/// real servers accept it. Confirm against a real sandbox account (OTP from fatoora.zatca.gov.sa) before
/// relying on this in production.
/// </summary>
public class ZatcaHttpApiClient : IZatcaApiClient
{
    private const string ApiVersion = "V2";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ISecretProtector _secretProtector;

    public ZatcaHttpApiClient(HttpClient httpClient, ISecretProtector secretProtector)
    {
        _httpClient = httpClient;
        _secretProtector = secretProtector;
    }

    private static string GetBaseUri(EInvoicingEnvironment environment) => environment switch
    {
        EInvoicingEnvironment.Sandbox => "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal",
        EInvoicingEnvironment.Simulation => "https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation",
        EInvoicingEnvironment.Production => "https://gw-fatoora.zatca.gov.sa/e-invoicing/core",
        _ => throw new ArgumentOutOfRangeException(nameof(environment)),
    };

    public async Task<ZatcaCsidResult> RequestComplianceCertificateAsync(string csrPem, string otp, CompanySettings settings, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string> { ["csr"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csrPem)) };
        var extraHeaders = new Dictionary<string, string> { ["OTP"] = otp };
        var json = await SendAsync(HttpMethod.Post, settings.EInvoicingEnvironment, "/compliance", extraHeaders, authHeader: null, body, ct);
        return ParseCsidResult(json, "طلب شهادة المطابقة (Compliance CSID)");
    }

    public async Task<ZatcaCsidResult> RequestProductionCertificateAsync(string complianceCertificatePem, string complianceSecret, string complianceRequestId, CompanySettings settings, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string> { ["compliance_request_id"] = complianceRequestId };
        var authHeader = BuildAuthHeader(complianceCertificatePem, complianceSecret);
        var json = await SendAsync(HttpMethod.Post, settings.EInvoicingEnvironment, "/production/csids", extraHeaders: null, authHeader, body, ct);
        return ParseCsidResult(json, "طلب الشهادة الإنتاجية (Production CSID)");
    }

    public async Task<ZatcaComplianceCheckResult> ValidateComplianceInvoiceAsync(
        string complianceCertificatePem, string complianceSecret, string signedInvoiceXml, string invoiceHash, string uuid,
        CompanySettings settings, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["invoiceHash"] = invoiceHash,
            ["uuid"] = uuid,
            ["invoice"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(signedInvoiceXml)),
        };
        var authHeader = BuildAuthHeader(complianceCertificatePem, complianceSecret);
        try
        {
            var json = await SendAsync(HttpMethod.Post, settings.EInvoicingEnvironment, "/compliance/invoices", extraHeaders: null, authHeader, body, ct);
            var status = json.TryGetProperty("clearanceStatus", out var s) ? s.GetString()
                : json.TryGetProperty("reportingStatus", out var r) ? r.GetString()
                : null;
            return new ZatcaComplianceCheckResult(true, status, null);
        }
        catch (ZatcaApiException ex)
        {
            return new ZatcaComplianceCheckResult(false, null, ex.Message);
        }
    }

    public Task<ZatcaSubmissionResponse> ReportSimplifiedInvoiceAsync(string signedXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default)
        => SubmitInvoiceAsync(signedXml, invoiceHash, uuid, settings, "/invoices/reporting/single", clearanceStatus: "0", ct);

    public Task<ZatcaSubmissionResponse> ClearStandardInvoiceAsync(string signedXml, string invoiceHash, string uuid, CompanySettings settings, CancellationToken ct = default)
        => SubmitInvoiceAsync(signedXml, invoiceHash, uuid, settings, "/invoices/clearance/single", clearanceStatus: "1", ct);

    private async Task<ZatcaSubmissionResponse> SubmitInvoiceAsync(
        string signedXml, string invoiceHash, string uuid, CompanySettings settings, string path, string clearanceStatus, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.EInvoicingCertificateEncrypted) || string.IsNullOrWhiteSpace(settings.EInvoicingClientSecretEncrypted))
            return new ZatcaSubmissionResponse(false, "لسه معندكش شهادة إنتاجية (Production CSID) — خلّص خطوات التسجيل الأول.");

        var certificatePem = _secretProtector.Unprotect(settings.EInvoicingCertificateEncrypted);
        var secret = _secretProtector.Unprotect(settings.EInvoicingClientSecretEncrypted);

        var body = new Dictionary<string, string>
        {
            ["invoiceHash"] = invoiceHash,
            ["uuid"] = uuid,
            ["invoice"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(signedXml)),
        };
        var extraHeaders = new Dictionary<string, string> { ["Clearance-Status"] = clearanceStatus };
        var authHeader = BuildAuthHeader(certificatePem, secret);

        try
        {
            await SendAsync(HttpMethod.Post, settings.EInvoicingEnvironment, path, extraHeaders, authHeader, body, ct);
            return new ZatcaSubmissionResponse(true, null);
        }
        catch (ZatcaApiException ex)
        {
            return new ZatcaSubmissionResponse(false, ex.Message);
        }
    }

    /// <summary>ZATCA's Basic-auth convention: base64( base64(DER certificate bytes) : secret ).</summary>
    private static (string Name, string Value) BuildAuthHeader(string certificatePem, string secret)
    {
        var certificate = X509Certificate2.CreateFromPem(certificatePem);
        var certificateBase64 = Convert.ToBase64String(certificate.RawData);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{certificateBase64}:{secret}"));
        return ("Authorization", $"Basic {credentials}");
    }

    private static ZatcaCsidResult ParseCsidResult(JsonElement json, string operationLabel)
    {
        var tokenBase64 = json.TryGetProperty("binarySecurityToken", out var t) ? t.GetString() : null;
        var secret = json.TryGetProperty("secret", out var s) ? s.GetString() : null;
        var requestId = json.TryGetProperty("requestID", out var r) ? r.GetString() : null;

        if (string.IsNullOrEmpty(tokenBase64) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(requestId))
            throw new ValidationAppException($"استجابة غير متوقعة من ZATCA أثناء {operationLabel} — البيانات المطلوبة (الشهادة/السر/رقم الطلب) مش موجودة في الرد.");

        var certificateDer = Convert.FromBase64String(tokenBase64);
        var certificatePem = PemEncoding.WriteString("CERTIFICATE", certificateDer);
        return new ZatcaCsidResult(certificatePem, secret, requestId);
    }

    private async Task<JsonElement> SendAsync(
        HttpMethod method, EInvoicingEnvironment environment, string path,
        Dictionary<string, string>? extraHeaders, (string Name, string Value)? authHeader,
        Dictionary<string, string> body, CancellationToken ct)
    {
        var url = GetBaseUri(environment) + path;
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.TryAddWithoutValidation("Accept-Version", ApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
        if (extraHeaders is not null)
            foreach (var (key, value) in extraHeaders)
                request.Headers.TryAddWithoutValidation(key, value);
        if (authHeader is { } auth)
            request.Headers.TryAddWithoutValidation(auth.Name, auth.Value);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ZatcaApiException($"تعذر الاتصال بمنظومة ZATCA ({path}): {ex.Message}");
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        JsonElement json = default;
        if (!string.IsNullOrWhiteSpace(content))
        {
            try { json = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions); }
            catch (JsonException) { /* leave default; status code check below still applies */ }
        }

        // 200 = success, 202 = accepted with warnings — anything else is an error.
        if ((int)response.StatusCode >= 300)
        {
            var detail = json.ValueKind == JsonValueKind.Undefined ? content : json.ToString();
            throw new ZatcaApiException($"خطأ من منظومة ZATCA (HTTP {(int)response.StatusCode}) على {path}: {detail}");
        }

        return json;
    }
}

public class ZatcaApiException : Exception
{
    public ZatcaApiException(string message) : base(message) { }
}
