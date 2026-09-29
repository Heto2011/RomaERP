using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public enum ContractType
{
    Permanent = 1,
    FixedTerm = 2,
    PartTime = 3
}

public enum EmployeeContractStatus
{
    Active = 1,
    /// <summary>Superseded by a newer contract for the same employee — set automatically when a new
    /// contract is created while this one was Active.</summary>
    Renewed = 2,
    Terminated = 3
}

/// <summary>One employment contract period for an employee. History is kept — a renewal creates a new row
/// rather than overwriting the old one, so past contract terms stay visible. Only one contract per employee
/// should be Active at a time; EmployeeContractService enforces this on create.</summary>
public class EmployeeContract : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public ContractType ContractType { get; set; }
    public DateTime StartDate { get; set; }

    /// <summary>Required for FixedTerm/PartTime, must stay null for Permanent.</summary>
    public DateTime? EndDate { get; set; }

    public EmployeeContractStatus Status { get; set; } = EmployeeContractStatus.Active;
    public string? Notes { get; set; }
}
