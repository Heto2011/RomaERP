using RomaERP.Application.Assistant.DTOs;

namespace RomaERP.Application.Assistant.Services;

public interface IBankReconciliationService
{
    Task<BankStatementImportDto> ImportAsync(Stream csvStream, string fileName, Guid bankAccountId, string userId, CancellationToken ct = default);
    Task<List<BankStatementLineDto>> GetUnmatchedLinesAsync(CancellationToken ct = default);
    Task<int> AutoMatchAsync(CancellationToken ct = default);
    Task<ExpenseCaptureDto> MatchManualAsync(ManualMatchDto dto, CancellationToken ct = default);
}
