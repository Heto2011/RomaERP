using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IPayrollService
{
    Task<List<PayrollRunDto>> GetAllAsync(CancellationToken ct = default);
    Task<PayrollRunDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> CreateAndCalculateAsync(CreatePayrollRunDto dto, CancellationToken ct = default);
    Task<PayrollRunDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PayrollRunDto> PostAsync(Guid id, CancellationToken ct = default);
    Task<List<MyPayslipDto>> GetMyPayslipsAsync(Guid employeeId, CancellationToken ct = default);
}
