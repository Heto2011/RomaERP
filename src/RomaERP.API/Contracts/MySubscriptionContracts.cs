namespace RomaERP.API.Contracts;

/// <summary>Where a customer sends a subscription payment. Filled from configuration
/// (BankTransfer:*), never hardcoded — see appsettings.json / deploy secrets.</summary>
public record BankTransferInfoDto(bool Configured, string? AccountName, string? Iban, string? Swift, string? BankName);

public record ReportInvoicePaymentRequest(string? PaymentReference, string? Note);
