using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.DTOs;

public class SalaryComponentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public CalculationType CalculationType { get; set; }
    public decimal DefaultValue { get; set; }
    public bool IsTaxable { get; set; }
    public Guid? LinkedAccountId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSalaryComponentDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public CalculationType CalculationType { get; set; }
    public decimal DefaultValue { get; set; }
    public bool IsTaxable { get; set; }
    public Guid? LinkedAccountId { get; set; }
}

public class PayrollRunLineDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }
}

public class PayrollRunDto
{
    public Guid Id { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public DateTime RunDate { get; set; }
    public PayrollRunStatus Status { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
    public List<PayrollRunLineDto> Lines { get; set; } = new();
    public decimal TotalNet => Lines.Sum(l => l.NetSalary);
}

public class CreatePayrollRunDto
{
    public Guid FiscalPeriodId { get; set; }
    public DateTime RunDate { get; set; }
    public string? Description { get; set; }
}
