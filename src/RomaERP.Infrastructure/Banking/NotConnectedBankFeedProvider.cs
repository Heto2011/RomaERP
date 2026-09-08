using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Banking;

/// <summary>Default <see cref="IBankFeedProvider"/>: no live aggregator has been chosen yet, so this
/// always reports <see cref="IsConfigured"/> = false and every fetch fails gracefully. Reconciliation
/// still works today through <c>IBankFeedReconciliationService.ImportCsvAsync</c> (manual bank-statement
/// upload) — this class is only the not-yet-connected placeholder for automatic syncing, swapped out for
/// a real provider (e.g. Lean Technologies, Tarabut Gateway) later with no other code changes, the same
/// way <c>MoyasarPaymentProvider</c> was swapped for <c>PayTabsPaymentProvider</c>.</summary>
public class NotConnectedBankFeedProvider : IBankFeedProvider
{
    public string Name => "غير متصل";
    public bool IsConfigured => false;

    public Task<BankFeedFetchResult> FetchTransactionsAsync(BankFeedFetchRequest request, CancellationToken ct = default)
        => Task.FromResult(new BankFeedFetchResult(false, new List<BankFeedTransactionData>(),
            "التغذية البنكية اللحظية غير مفعّلة بعد — لسه مفيش مزود Open Banking متصل. ارفع كشف الحساب (CSV) يدويًا لحد ما يتم توصيل مزود."));
}
