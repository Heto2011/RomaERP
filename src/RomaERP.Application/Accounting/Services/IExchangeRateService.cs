using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IExchangeRateService
{
    Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct = default);
    Task<ExchangeRateDto> SetRateAsync(SetExchangeRateDto dto, CancellationToken ct = default);

    /// <summary>Resolves a requested currency code (null/empty means "use the tenant's functional
    /// currency") to its actual code plus the rate to convert 1 unit of it into the tenant's functional
    /// currency, as of the given date. Returns (functionalCurrency, 1) when the requested currency already
    /// is the functional currency. Throws ValidationAppException if a foreign currency has no rate on or
    /// before that date.</summary>
    Task<(string CurrencyCode, decimal RateToFunctional)> ResolveAsync(string? requestedCurrencyCode, DateTime date, CancellationToken ct = default);
}
