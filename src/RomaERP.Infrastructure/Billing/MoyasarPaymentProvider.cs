using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Billing;

/// <summary>Charges a previously-saved card token through Moyasar (moyasar.com) — a Saudi gateway supporting
/// mada/Visa/Mastercard/Apple Pay. Inactive until <c>Moyasar:SecretKey</c> is set in configuration, so the
/// rest of the subscription system works in "Manual" mode before a gateway account exists.</summary>
public class MoyasarPaymentProvider : IPaymentGatewayProvider
{
    private const string BaseUrl = "https://api.moyasar.com/v1/";

    private readonly HttpClient _http;
    private readonly string? _secretKey;

    public MoyasarPaymentProvider(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
        _secretKey = configuration["Moyasar:SecretKey"];
    }

    public string Name => "Moyasar";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secretKey);

    public async Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new PaymentChargeResult(false, null, "Moyasar غير مفعّل — لسه مفيش Moyasar:SecretKey في الإعدادات.");

        if (string.IsNullOrWhiteSpace(request.TokenRef))
            return new PaymentChargeResult(false, null, "مفيش بطاقة محفوظة لهذا الاشتراك.");

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_secretKey}:")));

        // Moyasar bills in halalas (SAR x 100).
        var payload = new MoyasarChargeRequest((int)Math.Round(request.Amount * 100), request.Currency, request.Description, request.TokenRef);

        try
        {
            var response = await _http.PostAsJsonAsync("payments", payload, ct);
            var body = await response.Content.ReadFromJsonAsync<MoyasarPaymentResponse>(cancellationToken: ct);

            if (response.IsSuccessStatusCode && body is not null && string.Equals(body.Status, "paid", StringComparison.OrdinalIgnoreCase))
                return new PaymentChargeResult(true, body.Id, null);

            return new PaymentChargeResult(false, body?.Id, body?.Source?.Message ?? $"Moyasar returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new PaymentChargeResult(false, null, ex.Message);
        }
    }

    private record MoyasarChargeRequest(int Amount, string Currency, string Description, [property: JsonPropertyName("token")] string Token);
    private record MoyasarPaymentResponse(string Id, string Status, MoyasarSource? Source);
    private record MoyasarSource(string? Message);
}
