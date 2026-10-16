using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class ShiftDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>HH:mm</summary>
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "17:00";
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOvernight { get; set; }
}

public class RosterEntryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public Guid ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class SetShiftAssignmentDto
{
    public Guid EmployeeId { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Null clears the day (employee off).</summary>
    public Guid? ShiftId { get; set; }
}

public class CopyRosterWeekDto
{
    public DateTime FromStart { get; set; }
    public DateTime ToStart { get; set; }
}

public interface IShiftService
{
    Task<List<ShiftDto>> GetShiftsAsync(CancellationToken ct = default);
    Task<ShiftDto> CreateShiftAsync(ShiftDto dto, CancellationToken ct = default);
    Task<ShiftDto> UpdateShiftAsync(Guid id, ShiftDto dto, CancellationToken ct = default);
    Task DeleteShiftAsync(Guid id, CancellationToken ct = default);
    Task<List<RosterEntryDto>> GetRosterAsync(DateTime from, DateTime to, Guid? employeeId, CancellationToken ct = default);
    Task SetAssignmentAsync(SetShiftAssignmentDto dto, CancellationToken ct = default);
    /// <summary>Copies every assignment of the seven days starting at FromStart to the week starting at ToStart, replacing what is there. Returns how many days were copied.</summary>
    Task<int> CopyWeekAsync(CopyRosterWeekDto dto, CancellationToken ct = default);
}

public class ShiftService : IShiftService
{
    private readonly IApplicationDbContext _context;

    public ShiftService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ShiftDto>> GetShiftsAsync(CancellationToken ct = default)
    {
        var shifts = await _context.WorkShifts.AsNoTracking().OrderBy(s => s.StartTime).ThenBy(s => s.Name).ToListAsync(ct);
        return shifts.Select(Map).ToList();
    }

    public async Task<ShiftDto> CreateShiftAsync(ShiftDto dto, CancellationToken ct = default)
    {
        var shift = new WorkShift();
        Apply(shift, dto);
        _context.WorkShifts.Add(shift);
        await _context.SaveChangesAsync(ct);
        return Map(shift);
    }

    public async Task<ShiftDto> UpdateShiftAsync(Guid id, ShiftDto dto, CancellationToken ct = default)
    {
        var shift = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(WorkShift), id);
        Apply(shift, dto);
        await _context.SaveChangesAsync(ct);
        return Map(shift);
    }

    public async Task DeleteShiftAsync(Guid id, CancellationToken ct = default)
    {
        var shift = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(WorkShift), id);
        if (await _context.ShiftAssignments.AnyAsync(a => a.WorkShiftId == id, ct))
            throw new ValidationAppException("الوردية دي مستخدمة في الجدول. عطّلها بدل ما تحذفها.");
        shift.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<RosterEntryDto>> GetRosterAsync(DateTime from, DateTime to, Guid? employeeId, CancellationToken ct = default)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start || (end - start).TotalDays > 62)
            throw new ValidationAppException("المدة لازم تكون من يوم لحد شهرين كحد أقصى.");

        var query = _context.ShiftAssignments.AsNoTracking()
            .Include(a => a.Employee).Include(a => a.WorkShift)
            .Where(a => a.Date >= start && a.Date <= end);
        if (employeeId is { } id) query = query.Where(a => a.EmployeeId == id);

        var rows = await query.OrderBy(a => a.Date).ThenBy(a => a.Employee!.FullNameAr).ToListAsync(ct);
        return rows.Select(a => new RosterEntryDto
        {
            EmployeeId = a.EmployeeId,
            EmployeeName = a.Employee!.FullNameAr,
            Date = a.Date,
            ShiftId = a.WorkShiftId,
            ShiftName = a.WorkShift!.Name,
            StartTime = Format(a.WorkShift.StartTime),
            EndTime = Format(a.WorkShift.EndTime),
            Color = a.WorkShift.Color
        }).ToList();
    }

    public async Task SetAssignmentAsync(SetShiftAssignmentDto dto, CancellationToken ct = default)
    {
        var day = dto.Date.Date;
        if (!await _context.Employees.AnyAsync(e => e.Id == dto.EmployeeId && !e.IsDeleted, ct))
            throw new NotFoundException(nameof(Employee), dto.EmployeeId);

        var existing = await _context.ShiftAssignments.FirstOrDefaultAsync(a => a.EmployeeId == dto.EmployeeId && a.Date == day, ct);
        if (dto.ShiftId is not { } shiftId)
        {
            // The unique index covers soft-deleted rows too, so a cleared day is physically removed.
            if (existing is not null) _context.ShiftAssignments.Remove(existing);
            await _context.SaveChangesAsync(ct);
            return;
        }

        var shift = await _context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(WorkShift), shiftId);
        if (!shift.IsActive) throw new ValidationAppException("الوردية دي معطّلة.");

        if (existing is null)
            _context.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = dto.EmployeeId, WorkShiftId = shiftId, Date = day });
        else
            existing.WorkShiftId = shiftId;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> CopyWeekAsync(CopyRosterWeekDto dto, CancellationToken ct = default)
    {
        var from = dto.FromStart.Date;
        var to = dto.ToStart.Date;
        if (from == to) throw new ValidationAppException("الأسبوعين لازم يكونوا مختلفين.");

        var source = await _context.ShiftAssignments
            .Include(a => a.WorkShift)
            .Where(a => a.Date >= from && a.Date < from.AddDays(7) && !a.WorkShift!.IsDeleted && a.WorkShift.IsActive)
            .ToListAsync(ct);

        var offset = to - from;
        var target = await _context.ShiftAssignments.Where(a => a.Date >= to && a.Date < to.AddDays(7)).ToListAsync(ct);
        _context.ShiftAssignments.RemoveRange(target);
        await _context.SaveChangesAsync(ct);

        foreach (var a in source)
            _context.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = a.EmployeeId, WorkShiftId = a.WorkShiftId, Date = a.Date + offset });
        await _context.SaveChangesAsync(ct);
        return source.Count;
    }

    private static void Apply(WorkShift shift, ShiftDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationAppException("اكتب اسم الوردية.");
        shift.Name = dto.Name.Trim();
        shift.StartTime = Parse(dto.StartTime);
        shift.EndTime = Parse(dto.EndTime);
        if (shift.StartTime == shift.EndTime) throw new ValidationAppException("بداية ونهاية الوردية لازم يكونوا مختلفين.");
        shift.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();
        shift.IsActive = dto.IsActive;
    }

    private static TimeSpan Parse(string value)
        => TimeSpan.TryParseExact(value?.Trim(), new[] { "hh\\:mm", "h\\:mm" }, CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromHours(24)
            ? t
            : throw new ValidationAppException("اكتب الوقت بصيغة ساعة:دقيقة، مثلاً 08:00.");

    private static string Format(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";

    private static ShiftDto Map(WorkShift s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        StartTime = Format(s.StartTime),
        EndTime = Format(s.EndTime),
        Color = s.Color,
        IsActive = s.IsActive,
        IsOvernight = s.EndTime <= s.StartTime
    };
}
