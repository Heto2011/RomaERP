using RomaERP.Application.Restaurant.DTOs;

namespace RomaERP.Application.Restaurant.Services;

public interface ICashierShiftService
{
    Task<CashierShiftDto?> GetActiveShiftAsync(Guid employeeId, CancellationToken ct = default);
    Task<CashierShiftDto> OpenAsync(OpenCashierShiftDto dto, CancellationToken ct = default);
    Task<CashierShiftDto> CloseAsync(Guid shiftId, CloseCashierShiftDto dto, CancellationToken ct = default);
}
