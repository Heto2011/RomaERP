using RomaERP.Domain.Accounting;
using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public class SalaryComponent : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public CalculationType CalculationType { get; set; }
    public decimal DefaultValue { get; set; }
    public bool IsTaxable { get; set; }

    public Guid? LinkedAccountId { get; set; }
    public Account? LinkedAccount { get; set; }

    public bool IsActive { get; set; } = true;
}

public class EmployeeSalaryComponent : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid SalaryComponentId { get; set; }
    public SalaryComponent? SalaryComponent { get; set; }

    public decimal Value { get; set; }
}
