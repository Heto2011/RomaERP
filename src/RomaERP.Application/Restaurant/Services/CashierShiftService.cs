using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Restaurant.DTOs;
using RomaERP.Domain.Common;
using RomaERP.Domain.HR;
using RomaERP.Domain.Restaurant;

namespace RomaERP.Application.Restaurant.Services;

public class CashierShiftService : ICashierShiftService
{
    private readonly IApplicationDbContext _context;

    public CashierShiftService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CashierShiftDto?> GetActiveShiftAsync(Guid employeeId, CancellationToken ct = default)
    {
        var shift = await _context.CashierShifts
            .AsNoTracking()
            .Include(s => s.Employee)
            .Where(s => s.EmployeeId == employeeId && s.Status == CashierShiftStatus.Open)
            .OrderByDescending(s => s.OpenedAtUtc)
            .FirstOrDefaultAsync(ct);

        return shift is null ? null : Map(shift);
    }

    public async Task<CashierShiftDto> OpenAsync(OpenCashierShiftDto dto, CancellationToken ct = default)
    {
        if (dto.OpeningFloat < 0)
            throw new ValidationAppException("العهدة الافتتاحية لا يمكن أن تكون سالبة.");

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), dto.EmployeeId);

        var alreadyOpen = await _context.CashierShifts.AnyAsync(s => s.EmployeeId == dto.EmployeeId && s.Status == CashierShiftStatus.Open, ct);
        if (alreadyOpen)
            throw new ValidationAppException("عندك شيفت مفتوح بالفعل — لازم تقفله قبل ما تفتح واحد جديد.");

        var shift = new CashierShift
        {
            EmployeeId = employee.Id,
            OpenedAtUtc = DateTime.UtcNow,
            OpeningFloat = dto.OpeningFloat,
            Status = CashierShiftStatus.Open
        };

        _context.CashierShifts.Add(shift);
        await _context.SaveChangesAsync(ct);

        shift.Employee = employee;
        return Map(shift);
    }

    public async Task<CashierShiftDto> CloseAsync(Guid shiftId, CloseCashierShiftDto dto, CancellationToken ct = default)
    {
        var shift = await _context.CashierShifts
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw new NotFoundException(nameof(CashierShift), shiftId);

        if (shift.Status != CashierShiftStatus.Open)
            throw new ValidationAppException("الشيفت ده مقفول بالفعل.");

        var cashSalesTotal = await _context.RestaurantOrders
            .AsNoTracking()
            .Where(o => o.CashierShiftId == shiftId && o.SalesInvoice != null && o.SalesInvoice.PaymentTerm == PaymentTerm.Cash)
            .SumAsync(o => o.SalesInvoice!.TotalAmount, ct);

        shift.ExpectedCash = shift.OpeningFloat + cashSalesTotal;
        shift.ClosingCountedCash = dto.ClosingCountedCash;
        shift.CashVariance = dto.ClosingCountedCash - shift.ExpectedCash;
        shift.ClosedAtUtc = DateTime.UtcNow;
        shift.Status = CashierShiftStatus.Closed;

        await _context.SaveChangesAsync(ct);
        return Map(shift);
    }

    private static CashierShiftDto Map(CashierShift s) => new()
    {
        Id = s.Id,
        EmployeeId = s.EmployeeId,
        EmployeeName = s.Employee?.FullNameAr ?? string.Empty,
        OpenedAtUtc = s.OpenedAtUtc,
        OpeningFloat = s.OpeningFloat,
        ClosedAtUtc = s.ClosedAtUtc,
        ClosingCountedCash = s.ClosingCountedCash,
        ExpectedCash = s.ExpectedCash,
        CashVariance = s.CashVariance,
        Status = (int)s.Status
    };
}
