using RomaERP.Domain.Common;

namespace RomaERP.Domain.Accounting;

/// <summary>A manually-entered rate for converting a foreign currency into the tenant's functional
/// currency (CompanySettings.DefaultCurrency) as of a given date. Invoices and payments raised in a
/// foreign currency look up the latest rate on or before their own date to convert into the functional
/// currency for GL posting and AR/AP balances.</summary>
public class ExchangeRate : AuditableEntity
{
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }

    /// <summary>Units of the tenant's functional currency equal to 1 unit of CurrencyCode.</summary>
    public decimal RateToFunctional { get; set; }
}
