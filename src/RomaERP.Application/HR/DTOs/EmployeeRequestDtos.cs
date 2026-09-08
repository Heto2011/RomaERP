using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.DTOs;

public class EmployeeRequestDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public EmployeeRequestType Type { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Reason { get; set; }
    public EmployeeRequestStatus Status { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

public class CreateEmployeeRequestDto
{
    public EmployeeRequestType Type { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Reason { get; set; }
}

public class DecideEmployeeRequestDto
{
    public bool Approve { get; set; }
    public string? DecisionNote { get; set; }
}
