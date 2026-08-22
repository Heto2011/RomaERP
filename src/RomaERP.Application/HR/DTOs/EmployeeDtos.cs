using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.DTOs;

public class EmployeeDto
{
    public Guid Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public string FullNameEn { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public DateTime? BirthDate { get; set; }
    public Gender Gender { get; set; }
    public MaritalStatus MaritalStatus { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public Guid PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Iban { get; set; }
    public Guid? ApplicationUserId { get; set; }
}

public class CreateEmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public string FullNameEn { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public DateTime? BirthDate { get; set; }
    public Gender Gender { get; set; }
    public MaritalStatus MaritalStatus { get; set; }
    public DateTime HireDate { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid PositionId { get; set; }
    public decimal BasicSalary { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Iban { get; set; }
}

public class UpdateEmployeeDto : CreateEmployeeDto
{
    public EmploymentStatus EmploymentStatus { get; set; }
    public DateTime? TerminationDate { get; set; }
}
