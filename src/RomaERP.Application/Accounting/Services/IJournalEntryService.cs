using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IJournalEntryService
{
    Task<List<JournalEntryDto>> GetAllAsync(CancellationToken ct = default);
    Task<JournalEntryDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<JournalEntryDto> CreateAsync(CreateJournalEntryDto dto, CancellationToken ct = default);
    Task<JournalEntryDto> PostAsync(Guid id, CancellationToken ct = default);
    Task<JournalEntryDto> ReverseAsync(Guid id, CancellationToken ct = default);
    Task<List<TrialBalanceLineDto>> GetTrialBalanceAsync(DateTime? asOfDate, CancellationToken ct = default);
}
