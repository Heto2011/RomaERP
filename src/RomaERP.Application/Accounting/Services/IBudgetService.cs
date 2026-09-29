using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IBudgetService
{
    Task<List<BudgetLineDto>> GetBudgetLinesAsync(Guid fiscalYearId, CancellationToken ct = default);
    Task<BudgetLineDto> SetBudgetLineAsync(SetBudgetLineDto dto, CancellationToken ct = default);
    Task<BudgetVsActualReportDto> GetBudgetVsActualAsync(Guid fiscalYearId, CancellationToken ct = default);
}
