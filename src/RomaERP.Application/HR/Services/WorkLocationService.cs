using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class WorkLocationService : IWorkLocationService
{
    private readonly IApplicationDbContext _context;

    public WorkLocationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkLocationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var locations = await _context.WorkLocations
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

        return locations.Select(Map).ToList();
    }

    public async Task<WorkLocationDto> CreateAsync(SaveWorkLocationDto dto, CancellationToken ct = default)
    {
        Validate(dto);

        var location = new WorkLocation
        {
            Name = dto.Name.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            GeofenceRadiusMeters = dto.GeofenceRadiusMeters,
            IsActive = dto.IsActive
        };

        _context.WorkLocations.Add(location);
        await _context.SaveChangesAsync(ct);
        return Map(location);
    }

    public async Task<WorkLocationDto> UpdateAsync(Guid id, SaveWorkLocationDto dto, CancellationToken ct = default)
    {
        Validate(dto);

        var location = await _context.WorkLocations.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException(nameof(WorkLocation), id);

        location.Name = dto.Name.Trim();
        location.Latitude = dto.Latitude;
        location.Longitude = dto.Longitude;
        location.GeofenceRadiusMeters = dto.GeofenceRadiusMeters;
        location.IsActive = dto.IsActive;

        await _context.SaveChangesAsync(ct);
        return Map(location);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var location = await _context.WorkLocations.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException(nameof(WorkLocation), id);

        var hasEmployees = await _context.Employees.AnyAsync(e => e.WorkLocationId == id && !e.IsDeleted, ct);
        if (hasEmployees)
            throw new ValidationAppException("لا يمكن حذف موقع عمل مرتبط بموظفين — انقل الموظفين لموقع تاني الأول.");

        location.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    private static void Validate(SaveWorkLocationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationAppException("اسم موقع العمل مطلوب.");
        if (dto.Latitude is < -90 or > 90)
            throw new ValidationAppException("خط العرض (Latitude) لازم يكون بين -90 و90.");
        if (dto.Longitude is < -180 or > 180)
            throw new ValidationAppException("خط الطول (Longitude) لازم يكون بين -180 و180.");
        if (dto.GeofenceRadiusMeters <= 0)
            throw new ValidationAppException("نطاق الحضور (Geofence) لازم يكون رقم موجب.");
    }

    private static WorkLocationDto Map(WorkLocation w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        Latitude = w.Latitude,
        Longitude = w.Longitude,
        GeofenceRadiusMeters = w.GeofenceRadiusMeters,
        IsActive = w.IsActive
    };
}
