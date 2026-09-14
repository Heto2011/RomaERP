using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IBankFeedReconciliationService
{
    Task<ImportBankFeedCsvResultDto> ImportCsvAsync(Stream csvStream, Guid accountId, CancellationToken ct = default);
    Task<SyncLiveBankFeedResultDto> SyncLiveAsync(Guid accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<BankFeedReconciliationSummaryDto> GetSummaryAsync(Guid accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<int> AutoMatchAsync(Guid accountId, CancellationToken ct = default);
    Task<BankFeedTransactionDto> MatchManualAsync(ManualMatchBankFeedDto dto, CancellationToken ct = default);
    Task<BankFeedTransactionDto> UnmatchAsync(Guid bankFeedTransactionId, CancellationToken ct = default);
    BankFeedProviderStatusDto GetProviderStatus();
}
