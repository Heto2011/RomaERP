using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using RomaERP.Domain.EInvoicing;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.EInvoicing.Zatca;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Verifies ZatcaHttpApiClient builds requests matching the exact shape (URL, headers, JSON body,
/// Basic-auth encoding) this session read from the "Saleh7/php-zatca-xml" reference implementation — since
/// there's no network access to ZATCA's real servers in this environment to verify against directly. A request
/// shape matching the reference is necessary but not sufficient for ZATCA to actually accept it.</summary>
public class ZatcaHttpApiClientTests
{
    private static readonly string TestCertPem = CreateTestCertificatePem();

    private static string CreateTestCertificatePem()
    {
        using var ecdsa = ECDsa.Create(ECCurve.CreateFromOid(new Oid("1.3.132.0.10", "secp256k1")));
        var request = new CertificateRequest("CN=Test Cert", ecdsa, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.ExportCertificatePem();
    }

    private class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public HttpStatusCode StatusCode = HttpStatusCode.OK;
        public string ResponseBody = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(StatusCode) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
        }
    }

    private static (ZatcaHttpApiClient Client, RecordingHandler Handler) BuildClient(PlainTextSecretProtector protector)
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        return (new ZatcaHttpApiClient(httpClient, protector), handler);
    }

    private static CompanySettings SandboxSettings() => new()
    {
        CompanyNameAr = "شركة تجريبية",
        CompanyNameEn = "Test Co",
        Country = Country.SaudiArabia,
        VatRate = 0.15m,
        DefaultCurrency = "SAR",
        EInvoicingEnvironment = EInvoicingEnvironment.Sandbox,
    };

    [Fact]
    public async Task RequestComplianceCertificateAsync_SendsExpectedUrlHeadersAndBody()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        handler.ResponseBody = JsonSerializer.Serialize(new { binarySecurityToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("cert-der-bytes")), secret = "my-secret", requestID = "req-123" });

        var result = await client.RequestComplianceCertificateAsync("CSR-CONTENT", "123456", SandboxSettings());

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/compliance", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("V2", handler.LastRequest.Headers.GetValues("Accept-Version").Single());
        Assert.Equal("123456", handler.LastRequest.Headers.GetValues("OTP").Single());

        var body = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("CSR-CONTENT")), body.GetProperty("csr").GetString());

        Assert.Equal("my-secret", result.Secret);
        Assert.Equal("req-123", result.RequestId);
        Assert.Contains("BEGIN CERTIFICATE", result.CertificatePem);
    }

    [Fact]
    public async Task RequestProductionCertificateAsync_UsesBasicAuthOfBase64CertAndSecret()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        handler.ResponseBody = JsonSerializer.Serialize(new { binarySecurityToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("prod-cert")), secret = "prod-secret", requestID = "req-999" });

        await client.RequestProductionCertificateAsync(TestCertPem, "compliance-secret", "req-123", SandboxSettings());

        Assert.Equal("https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/production/csids", handler.LastRequest!.RequestUri!.ToString());
        var authHeader = handler.LastRequest.Headers.GetValues("Authorization").Single();
        Assert.StartsWith("Basic ", authHeader);

        // ZATCA's Basic-auth convention: base64( base64(DER certificate bytes) : secret )
        var certificate = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(TestCertPem);
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Convert.ToBase64String(certificate.RawData)}:compliance-secret"));
        Assert.Equal($"Basic {expectedCredentials}", authHeader);

        var body = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("req-123", body.GetProperty("compliance_request_id").GetString());
    }

    [Fact]
    public async Task ClearStandardInvoiceAsync_SendsClearanceStatusOneAndInvoiceFields()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = SandboxSettings();
        settings.EInvoicingCertificateEncrypted = protector.Protect(TestCertPem);
        settings.EInvoicingClientSecretEncrypted = protector.Protect("prod-secret");

        var response = await client.ClearStandardInvoiceAsync("<Invoice/>", "hash-value", "uuid-value", settings);

        Assert.True(response.Success);
        Assert.Equal("https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/invoices/clearance/single", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("1", handler.LastRequest.Headers.GetValues("Clearance-Status").Single());

        var body = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal("hash-value", body.GetProperty("invoiceHash").GetString());
        Assert.Equal("uuid-value", body.GetProperty("uuid").GetString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("<Invoice/>")), body.GetProperty("invoice").GetString());
    }

    [Fact]
    public async Task ReportSimplifiedInvoiceAsync_SendsClearanceStatusZero()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = SandboxSettings();
        settings.EInvoicingCertificateEncrypted = protector.Protect(TestCertPem);
        settings.EInvoicingClientSecretEncrypted = protector.Protect("prod-secret");

        await client.ReportSimplifiedInvoiceAsync("<Invoice/>", "hash-value", "uuid-value", settings);

        Assert.Equal("0", handler.LastRequest!.Headers.GetValues("Clearance-Status").Single());
        Assert.Equal("https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/invoices/reporting/single", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task SubmitInvoice_WithoutStoredCredentials_ReturnsFailureWithoutCallingNetwork()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        var settings = SandboxSettings(); // no certificate/secret configured

        var response = await client.ClearStandardInvoiceAsync("<Invoice/>", "hash-value", "uuid-value", settings);

        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task RequestComplianceCertificateAsync_OnHttpErrorStatus_ThrowsWithDetail()
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        handler.StatusCode = HttpStatusCode.BadRequest;
        handler.ResponseBody = JsonSerializer.Serialize(new { message = "Invalid OTP" });

        var ex = await Assert.ThrowsAsync<ZatcaApiException>(
            () => client.RequestComplianceCertificateAsync("CSR", "000000", SandboxSettings()));
        Assert.Contains("400", ex.Message);
    }

    [Theory]
    [InlineData(EInvoicingEnvironment.Sandbox, "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/compliance")]
    [InlineData(EInvoicingEnvironment.Simulation, "https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation/compliance")]
    [InlineData(EInvoicingEnvironment.Production, "https://gw-fatoora.zatca.gov.sa/e-invoicing/core/compliance")]
    public async Task EnvironmentBaseUrls_MatchZatcaReferenceImplementation(EInvoicingEnvironment environment, string expectedUrl)
    {
        var protector = new PlainTextSecretProtector();
        var (client, handler) = BuildClient(protector);
        handler.ResponseBody = JsonSerializer.Serialize(new { binarySecurityToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("cert")), secret = "s", requestID = "r" });
        var settings = SandboxSettings();
        settings.EInvoicingEnvironment = environment;

        await client.RequestComplianceCertificateAsync("CSR", "000000", settings);

        Assert.Equal(expectedUrl, handler.LastRequest!.RequestUri!.ToString());
    }
}
