using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

/// <summary>A physical site (branch, warehouse, site office…) an employee clocks in at. Attendance
/// check-in/out compares the employee's GPS position against this location's coordinates within
/// <see cref="GeofenceRadiusMeters"/> to confirm they were actually there.</summary>
public class WorkLocation : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 20;
    public bool IsActive { get; set; } = true;
}
