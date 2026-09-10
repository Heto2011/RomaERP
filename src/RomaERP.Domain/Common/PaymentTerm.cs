namespace RomaERP.Domain.Common;

/// <summary>How an invoice is settled: paid immediately in cash/card, deferred to the customer's/vendor's
/// running account (Credit), or deferred on a fixed installment schedule (Installment). Installment posts to
/// Accounts Receivable exactly like Credit — the schedule is purely for collections tracking, not a different
/// GL treatment.</summary>
public enum PaymentTerm
{
    Cash = 1,
    Card = 2,
    Credit = 3,
    Installment = 4
}
