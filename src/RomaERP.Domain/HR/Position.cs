using RomaERP.Domain.Common;

namespace RomaERP.Domain.HR;

public class Position : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
