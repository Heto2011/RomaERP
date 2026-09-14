using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public enum EmployeeRequestType
{
    Leave = 1,
    Permission = 2,
    Other = 3
}

public enum EmployeeRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>An employee's self-service leave/permission request. An approved <see cref="EmployeeRequestType.Leave"/>
/// request is what payroll reads to auto-deduct unpaid days (see PayrollService.CreateAndCalculateAsync).</summary>
public class EmployeeRequest : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public EmployeeRequestType Type { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Reason { get; set; }
    public EmployeeRequestStatus Status { get; set; } = EmployeeRequestStatus.Pending;

    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}
