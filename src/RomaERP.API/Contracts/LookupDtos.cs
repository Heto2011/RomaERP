namespace RomaERP.API.Contracts;

public record FiscalPeriodLookupDto(Guid Id, string Name, DateTime StartDate, DateTime EndDate, bool IsClosed);

public record CostCenterLookupDto(Guid Id, string Code, string NameAr);

public record CompanySettingsLookupDto(decimal VatRate, string DefaultCurrency);
