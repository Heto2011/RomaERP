namespace RomaERP.Domain.EInvoicing;

public enum EInvoicingProvider
{
    None = 0,
    Eta = 1,
    Zatca = 2
}

public enum EInvoicingEnvironment
{
    Sandbox = 1,
    Production = 2
}

public enum EInvoiceStatus
{
    NotSubmitted = 1,
    Submitted = 2,
    Accepted = 3,
    Rejected = 4
}
