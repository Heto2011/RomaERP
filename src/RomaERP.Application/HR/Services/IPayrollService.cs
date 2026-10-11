using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IPayrollService
{
    Task<List<PayrollRunDto>> GetAllAsync(CancellationToken ct = default);
    Task<PayrollRunDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> CreateAndCalculateAsync(CreatePayrollRunDto dto, CancellationToken ct = default);
    Task<PayrollRunDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> RevertToDraftAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> PostAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> UpdateLineAsync(Guid runId, Guid employeeId, UpdatePayrollLineDto dto, CancellationToken ct = default);
    Task<List<MyPayslipDto>> GetMyPayslipsAsync(Guid employeeId, CancellationToken ct = default);
    /// <summary>The accountant's summary of a UK pay run as a CSV (one row per employee plus totals), for filing with HMRC outside the system.</summary>
    Task<string> BuildUkSummaryCsvAsync(Guid runId, CancellationToken ct = default);
    Task<PayrollSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<PayrollSettingsDto> UpdateSettingsAsync(PayrollSettingsDto dto, CancellationToken ct = default);
}
