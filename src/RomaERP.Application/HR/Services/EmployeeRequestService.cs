using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class EmployeeRequestService : IEmployeeRequestService
{
    private readonly IApplicationDbContext _context;

    public EmployeeRequestService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeRequestDto> CreateAsync(Guid employeeId, CreateEmployeeRequestDto dto, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        if (dto.DateTo is { } dateTo && dateTo < dto.DateFrom)
            throw new ValidationAppException("تاريخ النهاية لازم يكون بعد أو يساوي تاريخ البداية.");

        var request = new EmployeeRequest
        {
            EmployeeId = employeeId,
            Type = dto.Type,
            DateFrom = dto.DateFrom.Date,
            DateTo = dto.DateTo?.Date,
            Reason = dto.Reason,
            Status = EmployeeRequestStatus.Pending
        };

        _context.EmployeeRequests.Add(request);
        await _context.SaveChangesAsync(ct);

        return Map(request, employee);
    }

    public async Task<List<EmployeeRequestDto>> GetMineAsync(Guid employeeId, CancellationToken ct = default)
    {
        var requests = await _context.EmployeeRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.DateFrom)
            .ToListAsync(ct);

        return requests.Select(r => Map(r, r.Employee)).ToList();
    }

    public async Task<List<EmployeeRequestDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var requests = await _context.EmployeeRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .Where(r => r.Status == EmployeeRequestStatus.Pending)
            .OrderBy(r => r.DateFrom)
            .ToListAsync(ct);

        return requests.Select(r => Map(r, r.Employee)).ToList();
    }

    public async Task<List<EmployeeRequestDto>> GetAllAsync(CancellationToken ct = default)
    {
        var requests = await _context.EmployeeRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .OrderByDescending(r => r.DateFrom)
            .ToListAsync(ct);

        return requests.Select(r => Map(r, r.Employee)).ToList();
    }

    public async Task<EmployeeRequestDto> DecideAsync(Guid id, Guid decidedByUserId, DecideEmployeeRequestDto dto, CancellationToken ct = default)
    {
        var request = await _context.EmployeeRequests
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(EmployeeRequest), id);

        if (request.Status != EmployeeRequestStatus.Pending)
            throw new ValidationAppException("الطلب اتبت فيه بالفعل.");

        request.Status = dto.Approve ? EmployeeRequestStatus.Approved : EmployeeRequestStatus.Rejected;
        request.DecidedByUserId = decidedByUserId;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.DecisionNote = dto.DecisionNote;

        await _context.SaveChangesAsync(ct);
        return Map(request, request.Employee);
    }

    public async Task<LeaveBalanceDto> GetLeaveBalanceAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        var year = DateTime.UtcNow.Year;
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        var approvedInYear = await _context.EmployeeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId
                        && (r.Type == EmployeeRequestType.Leave || r.Type == EmployeeRequestType.Sickness)
                        && r.Status == EmployeeRequestStatus.Approved
                        && r.DateFrom <= yearEnd
                        && (r.DateTo ?? r.DateFrom) >= yearStart)
            .ToListAsync(ct);

        int DaysWithinYear(EmployeeRequest r)
        {
            var from = r.DateFrom < yearStart ? yearStart : r.DateFrom;
            var to = (r.DateTo ?? r.DateFrom) > yearEnd ? yearEnd : (r.DateTo ?? r.DateFrom);
            return (to.Date - from.Date).Days + 1;
        }

        var usedDays = approvedInYear.Where(r => r.Type == EmployeeRequestType.Leave).Sum(DaysWithinYear);
        var sicknessDays = approvedInYear.Where(r => r.Type == EmployeeRequestType.Sickness).Sum(DaysWithinYear);

        return new LeaveBalanceDto
        {
            Year = year,
            AnnualLeaveDaysPerYear = employee.AnnualLeaveDaysPerYear,
            UsedDays = usedDays,
            RemainingDays = employee.AnnualLeaveDaysPerYear - usedDays,
            SicknessDaysTaken = sicknessDays
        };
    }

    public async Task<List<CalendarEntryDto>> GetCalendarAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var from0 = from.Date;
        var to0 = to.Date;

        var entries = await _context.EmployeeRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .Where(r => (r.Type == EmployeeRequestType.Leave || r.Type == EmployeeRequestType.Sickness)
                        && r.Status == EmployeeRequestStatus.Approved
                        && r.DateFrom <= to0
                        && (r.DateTo ?? r.DateFrom) >= from0)
            .ToListAsync(ct);

        return entries.Select(r => new CalendarEntryDto
        {
            EmployeeName = r.Employee?.FullNameAr ?? string.Empty,
            Type = r.Type,
            DateFrom = r.DateFrom,
            DateTo = r.DateTo ?? r.DateFrom
        }).ToList();
    }

    private static EmployeeRequestDto Map(EmployeeRequest r, Employee? employee) => new()
    {
        Id = r.Id,
        EmployeeId = r.EmployeeId,
        EmployeeName = employee?.FullNameAr ?? string.Empty,
        Type = r.Type,
        DateFrom = r.DateFrom,
        DateTo = r.DateTo,
        Reason = r.Reason,
        Status = r.Status,
        DecidedAtUtc = r.DecidedAtUtc,
        DecisionNote = r.DecisionNote
    };
}
