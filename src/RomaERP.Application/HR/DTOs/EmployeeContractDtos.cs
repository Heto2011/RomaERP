using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.DTOs;

public class EmployeeContractDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public ContractType ContractType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public EmployeeContractStatus Status { get; set; }
    public string? Notes { get; set; }
    /// <summary>Null for a Permanent contract (no EndDate). Negative once EndDate has already passed —
    /// the stored Status stays whatever it was last set to until someone acts on it (renew or terminate),
    /// so this is how the UI flags an overdue contract without a background job re-stamping rows.</summary>
    public int? DaysUntilExpiry { get; set; }
}

public class CreateEmployeeContractDto
{
    public Guid EmployeeId { get; set; }
    public ContractType ContractType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateEmployeeContractStatusDto
{
    public EmployeeContractStatus Status { get; set; }
}
