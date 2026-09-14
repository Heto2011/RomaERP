namespace RomaERP.Application.Common.Interfaces;

public record BankFeedTransactionData(string ExternalId, DateTime TransactionDate, string Description, decimal Amount);

public record BankFeedFetchRequest(Guid AccountId, DateTime FromDate, DateTime ToDate);

public record BankFeedFetchResult(bool Success, List<BankFeedTransactionData> Transactions, string? FailureReason);

/// <summary>A pluggable live bank-feed source (an Open Banking aggregator such as Lean Technologies or
/// Tarabut Gateway, or a bank's own API) that can pull a GL bank account's real transactions automatically.
/// Reconciliation works today without this — <see cref="Services.IBankFeedReconciliationService.ImportCsvAsync"/>
/// lets an accountant upload a bank statement by hand — exactly like a subscription stays on "Manual"
/// billing until a payment gateway is configured. This interface is the seam: once a real aggregator is
/// chosen, add one implementation of it (see <c>PayTabsPaymentProvider</c> for the shape to follow) and
/// register it in place of <see cref="Infrastructure.Banking.NotConnectedBankFeedProvider"/> — no other
/// code changes needed.</summary>
public interface IBankFeedProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<BankFeedFetchResult> FetchTransactionsAsync(BankFeedFetchRequest request, CancellationToken ct = default);
}
