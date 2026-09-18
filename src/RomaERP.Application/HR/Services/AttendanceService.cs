using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IApplicationDbContext _context;
    private readonly IFaceVerificationProvider _faceProvider;

    public AttendanceService(IApplicationDbContext context, IFaceVerificationProvider faceProvider)
    {
        _context = context;
        _faceProvider = faceProvider;
    }

    public async Task<AttendanceRecordDto> CheckInAsync(Guid employeeId, decimal latitude, decimal longitude, byte[]? referencePhotoBytes, byte[]? selfieBytes, CancellationToken ct = default)
    {
        var employee = await _context.Employees
            .Include(e => e.WorkLocation)
            .FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        var alreadyOpen = await _context.AttendanceRecords
            .AnyAsync(a => a.EmployeeId == employeeId && a.CheckOutAtUtc == null, ct);
        if (alreadyOpen)
            throw new ValidationAppException("عندك حضور مفتوح بالفعل — سجّل انصراف الأول.");

        var withinGeofence = IsWithinGeofence(employee.WorkLocation, latitude, longitude);
        var (faceVerified, similarity) = await VerifyFaceAsync(referencePhotoBytes, selfieBytes, ct);

        var record = new AttendanceRecord
        {
            EmployeeId = employeeId,
            CheckInAtUtc = DateTime.UtcNow,
            CheckInLatitude = latitude,
            CheckInLongitude = longitude,
            CheckInWithinGeofence = withinGeofence,
            CheckInFaceVerified = faceVerified,
            CheckInFaceSimilarityPercent = similarity
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync(ct);

        return Map(record, employee);
    }

    public async Task<AttendanceRecordDto> CheckOutAsync(Guid employeeId, decimal latitude, decimal longitude, byte[]? referencePhotoBytes, byte[]? selfieBytes, CancellationToken ct = default)
    {
        var employee = await _context.Employees
            .Include(e => e.WorkLocation)
            .FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        var record = await _context.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.CheckOutAtUtc == null)
            .OrderByDescending(a => a.CheckInAtUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationAppException("مفيش تسجيل حضور مفتوح لهذا الموظف.");

        var withinGeofence = IsWithinGeofence(employee.WorkLocation, latitude, longitude);
        var (faceVerified, similarity) = await VerifyFaceAsync(referencePhotoBytes, selfieBytes, ct);

        record.CheckOutAtUtc = DateTime.UtcNow;
        record.CheckOutLatitude = latitude;
        record.CheckOutLongitude = longitude;
        record.CheckOutWithinGeofence = withinGeofence;
        record.CheckOutFaceVerified = faceVerified;
        record.CheckOutFaceSimilarityPercent = similarity;

        await _context.SaveChangesAsync(ct);
        return Map(record, employee);
    }

    public async Task<List<AttendanceRecordDto>> GetMineAsync(Guid employeeId, DateTime? from, DateTime? to, CancellationToken ct = default)
        => await QueryAsync(employeeId, from, to, ct);

    public async Task<List<AttendanceRecordDto>> GetAllAsync(DateTime? from, DateTime? to, Guid? employeeId, CancellationToken ct = default)
        => await QueryAsync(employeeId, from, to, ct);

    private async Task<List<AttendanceRecordDto>> QueryAsync(Guid? employeeId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var query = _context.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Employee)
            .AsQueryable();

        if (employeeId is { } id)
            query = query.Where(a => a.EmployeeId == id);
        if (from is { } fromDate)
            query = query.Where(a => a.CheckInAtUtc >= fromDate);
        if (to is { } toDate)
            query = query.Where(a => a.CheckInAtUtc <= toDate);

        var records = await query.OrderByDescending(a => a.CheckInAtUtc).ToListAsync(ct);
        return records.Select(r => Map(r, r.Employee)).ToList();
    }

    private static bool IsWithinGeofence(WorkLocation? location, decimal latitude, decimal longitude)
    {
        if (location is null)
            return false;

        var distance = GeoDistance.Meters(latitude, longitude, location.Latitude, location.Longitude);
        return distance <= location.GeofenceRadiusMeters;
    }

    private async Task<(bool? verified, decimal? similarity)> VerifyFaceAsync(byte[]? referencePhotoBytes, byte[]? selfieBytes, CancellationToken ct)
    {
        if (!_faceProvider.IsConfigured || referencePhotoBytes is null || selfieBytes is null)
            return (null, null);

        var result = await _faceProvider.CompareFacesAsync(referencePhotoBytes, selfieBytes, ct);
        return result.Success ? (result.IsMatch, result.SimilarityPercent) : (false, (decimal?)null);
    }

    private static AttendanceRecordDto Map(AttendanceRecord r, Employee? employee) => new()
    {
        Id = r.Id,
        EmployeeId = r.EmployeeId,
        EmployeeName = employee?.FullNameAr ?? string.Empty,
        CheckInAtUtc = r.CheckInAtUtc,
        CheckInWithinGeofence = r.CheckInWithinGeofence,
        CheckInFaceVerified = r.CheckInFaceVerified,
        CheckInFaceSimilarityPercent = r.CheckInFaceSimilarityPercent,
        CheckOutAtUtc = r.CheckOutAtUtc,
        CheckOutWithinGeofence = r.CheckOutWithinGeofence,
        CheckOutFaceVerified = r.CheckOutFaceVerified,
        CheckOutFaceSimilarityPercent = r.CheckOutFaceSimilarityPercent
    };
}
