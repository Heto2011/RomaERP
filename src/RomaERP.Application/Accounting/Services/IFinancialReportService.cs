using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IFinancialReportService
{
    Task<IncomeStatementDto> GetIncomeStatementAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate, CancellationToken ct = default);
    Task<CostCenterAnalysisDto> GetCostCenterAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
