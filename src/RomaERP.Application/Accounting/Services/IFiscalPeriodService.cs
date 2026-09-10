using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IFiscalPeriodService
{
    Task<List<FiscalYearDto>> GetAllYearsAsync(CancellationToken ct = default);
    Task<FiscalPeriodDto> ClosePeriodAsync(Guid periodId, CancellationToken ct = default);
    Task<FiscalPeriodDto> ReopenPeriodAsync(Guid periodId, CancellationToken ct = default);
    Task<FiscalYearDto> CloseFiscalYearAsync(Guid fiscalYearId, CancellationToken ct = default);
}
