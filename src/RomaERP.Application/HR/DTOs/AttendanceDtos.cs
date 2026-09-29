namespace RomaERP.Application.HR.DTOs;

public class AttendanceRecordDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public DateTime CheckInAtUtc { get; set; }
    public bool CheckInWithinGeofence { get; set; }
    public bool? CheckInFaceVerified { get; set; }
    public decimal? CheckInFaceSimilarityPercent { get; set; }

    public DateTime? CheckOutAtUtc { get; set; }
    public bool? CheckOutWithinGeofence { get; set; }
    public bool? CheckOutFaceVerified { get; set; }
    public decimal? CheckOutFaceSimilarityPercent { get; set; }

    /// <summary>True only once both check-in and check-out (when present) were inside the geofence — a
    /// quick "was this attendance legitimate" flag for managers reviewing the list. Face verification is
    /// informational only (see IFaceVerificationProvider's inert-until-configured doc) and doesn't affect
    /// this flag, since most tenants won't have it configured yet.</summary>
    public bool IsWithinGeofence => CheckInWithinGeofence && (CheckOutWithinGeofence ?? true);
}
