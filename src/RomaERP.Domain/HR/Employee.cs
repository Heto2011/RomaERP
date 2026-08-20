using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public class Employee : AuditableEntity
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public string FullNameEn { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public DateTime? BirthDate { get; set; }
    public Gender Gender { get; set; }
    public MaritalStatus MaritalStatus { get; set; }

    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid PositionId { get; set; }
    public Position? Position { get; set; }

    public decimal BasicSalary { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Iban { get; set; }

    public ICollection<EmployeeSalaryComponent> SalaryComponents { get; set; } = new List<EmployeeSalaryComponent>();
}
