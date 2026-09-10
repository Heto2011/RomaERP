namespace RomaERP.Application.Accounting.Services;

/// <summary>A live market-rate lookup, pluggable so the concrete data source (a free API today, a paid
/// one later) can change without touching ExchangeRateService.</summary>
public interface IExchangeRateProvider
{
    /// <summary>Units of toCurrencyCode equal to 1 unit of fromCurrencyCode, as of today. Returns null if
    /// the provider has no rate for this pair (unknown currency code, API unreachable, etc.) — the caller
    /// decides how to handle that (fall back to the last known rate, ask the user to enter one manually).</summary>
    Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, CancellationToken ct = default);
}
