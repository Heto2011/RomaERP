using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

/// <summary>A named working pattern (e.g. "Morning 08:00–16:00"). When the end time is not after the start time, the shift runs overnight into the next day.</summary>
public class WorkShift : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    /// <summary>Display colour on the roster, e.g. "#2f7d6d".</summary>
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Who works which shift on which day. One shift per employee per day; no row means the employee is off.</summary>
public class ShiftAssignment : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid WorkShiftId { get; set; }
    public WorkShift? WorkShift { get; set; }

    /// <summary>Calendar day (time part always midnight).</summary>
    public DateTime Date { get; set; }
}
