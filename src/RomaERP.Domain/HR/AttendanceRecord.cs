using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

/// <summary>One employee's clock-in/clock-out for a shift, captured from their own device: GPS position
/// (compared against their <see cref="WorkLocation"/>'s geofence) and, once a face-verification provider
/// is configured, a selfie compared against their stored reference photo.</summary>
public class AttendanceRecord : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime CheckInAtUtc { get; set; }
    public decimal CheckInLatitude { get; set; }
    public decimal CheckInLongitude { get; set; }
    public bool CheckInWithinGeofence { get; set; }
    /// <summary>Null when face verification isn't configured yet (see IFaceVerificationProvider) — the
    /// check-in still succeeds on GPS alone.</summary>
    public bool? CheckInFaceVerified { get; set; }
    public decimal? CheckInFaceSimilarityPercent { get; set; }

    public DateTime? CheckOutAtUtc { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public bool? CheckOutWithinGeofence { get; set; }
    public bool? CheckOutFaceVerified { get; set; }
    public decimal? CheckOutFaceSimilarityPercent { get; set; }
}
