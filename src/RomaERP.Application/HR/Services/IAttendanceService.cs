using RomaERP.Application.HR.DTOs;

namespace RomaERP.Application.HR.Services;

public interface IAttendanceService
{
    Task<AttendanceRecordDto> CheckInAsync(Guid employeeId, decimal latitude, decimal longitude, byte[]? referencePhotoBytes, byte[]? selfieBytes, CancellationToken ct = default);
    Task<AttendanceRecordDto> CheckOutAsync(Guid employeeId, decimal latitude, decimal longitude, byte[]? referencePhotoBytes, byte[]? selfieBytes, CancellationToken ct = default);
    Task<List<AttendanceRecordDto>> GetMineAsync(Guid employeeId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<AttendanceRecordDto>> GetAllAsync(DateTime? from, DateTime? to, Guid? employeeId, CancellationToken ct = default);
}
