using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IExchangeRateService
{
    Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct = default);
    Task<ExchangeRateDto> SetRateAsync(SetExchangeRateDto dto, CancellationToken ct = default);

    /// <summary>Starts tracking a foreign currency automatically: fetches today's live rate immediately
    /// (see IExchangeRateProvider) and stores it — no manual number entry. Throws ValidationAppException
    /// if the live provider has no rate for this currency code right now.</summary>
    Task<ExchangeRateDto> AddTrackedCurrencyAsync(string currencyCode, CancellationToken ct = default);

    /// <summary>Refreshes today's rate for every currency already tracked (has at least one row) from the
    /// live provider, skipping any currency whose today's row was a manual override — called both by an
    /// explicit "refresh now" request and by the automatic background job.</summary>
    Task RefreshAllTrackedCurrenciesAsync(CancellationToken ct = default);

    /// <summary>Resolves a requested currency code (null/empty means "use the tenant's functional
    /// currency") to its actual code plus the rate to convert 1 unit of it into the tenant's functional
    /// currency, as of the given date. Returns (functionalCurrency, 1) when the requested currency already
    /// is the functional currency. Throws ValidationAppException if a foreign currency has no rate on or
    /// before that date.</summary>
    Task<(string CurrencyCode, decimal RateToFunctional)> ResolveAsync(string? requestedCurrencyCode, DateTime date, CancellationToken ct = default);
}
