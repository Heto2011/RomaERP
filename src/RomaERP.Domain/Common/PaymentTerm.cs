namespace RomaERP.Domain.Common;

/// <summary>How an invoice is settled: paid immediately in cash/card, or deferred to the customer's/vendor's running account.</summary>
public enum PaymentTerm
{
    Cash = 1,
    Card = 2,
    Credit = 3
}
