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

/// <summary>Annual leave entitlement minus approved Leave-type requests already taken within the given
/// calendar year — a simple balance, not a month-by-month accrual.</summary>
public class LeaveBalanceDto
{
    public int Year { get; set; }
    public int AnnualLeaveDaysPerYear { get; set; }
    public int UsedDays { get; set; }
    public int RemainingDays { get; set; }
}
