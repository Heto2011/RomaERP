namespace RomaERP.Application.Accounting.DTOs;

public class ExchangeRateDto
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }
    public decimal RateToFunctional { get; set; }
    public string Source { get; set; } = "Manual";
}

public class SetExchangeRateDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }
    public decimal RateToFunctional { get; set; }
}

/// <summary>Starts automatic tracking of a foreign currency: the live rate is fetched immediately and
/// kept refreshed automatically from then on (see IExchangeRateService.RefreshAllTrackedCurrenciesAsync) —
/// no manual number entry needed.</summary>
public class AddTrackedCurrencyDto
{
    public string CurrencyCode { get; set; } = string.Empty;
}
