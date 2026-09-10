using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IOpeningBalanceService
{
    Task<JournalEntryDto?> GetForFiscalYearAsync(Guid fiscalYearId, CancellationToken ct = default);
    Task<JournalEntryDto> CreateAsync(CreateOpeningBalanceDto dto, CancellationToken ct = default);
}
