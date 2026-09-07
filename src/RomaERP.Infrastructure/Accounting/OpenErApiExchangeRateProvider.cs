using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RomaERP.Application.Accounting.Services;

namespace RomaERP.Infrastructure.Accounting;

/// <summary>Live exchange rates from open.er-api.com — free, no API key, ~160 currencies, refreshed once
/// daily on their end. Good enough for invoicing (the rate only needs to be right to the day, not the
/// second) without needing a paid FX data subscription before the business has any revenue.</summary>
public class OpenErApiExchangeRateProvider : IExchangeRateProvider
{
    private const string BaseUrl = "https://open.er-api.com/v6/latest/";

    private readonly HttpClient _http;

    public OpenErApiExchangeRateProvider(HttpClient http)
    {
        _http = http;
    }

    public async Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<OpenErApiResponse>($"{BaseUrl}{fromCurrencyCode}", ct);
            if (response is null || !string.Equals(response.Result, "success", StringComparison.OrdinalIgnoreCase) || response.Rates is null)
                return null;

            return response.Rates.TryGetValue(toCurrencyCode, out var rate) ? rate : null;
        }
        catch
        {
            return null;
        }
    }

    private class OpenErApiResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
