namespace RomaERP.Application.Accounting.DTOs;

public class ExchangeRateDto
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }
    public decimal RateToFunctional { get; set; }
}

public class SetExchangeRateDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }
    public decimal RateToFunctional { get; set; }
}
