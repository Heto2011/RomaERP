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

    /// <summary>Login account linked to this employee, if any — lets them see only their own profile and payslips.</summary>
    public Guid? ApplicationUserId { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid PositionId { get; set; }
    public Position? Position { get; set; }

    public decimal BasicSalary { get; set; }

    public Guid? WorkLocationId { get; set; }
    public WorkLocation? WorkLocation { get; set; }

    /// <summary>Stored filename of the reference photo used to verify attendance selfies against
    /// (App_Data/employee-faces/) — set via EmployeesController's photo upload. Null until uploaded, and
    /// face verification is skipped (GPS-only) for a check-in until it exists.</summary>
    public string? FaceReferencePhotoPath { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Iban { get; set; }

    /// <summary>Running balance of the employee's custody advance (عهدة) — increases when issued, decreases as approved custody-funded expenses are posted.</summary>
    public decimal CustodyBalance { get; set; }

    /// <summary>Drives GOSI eligibility in payroll (Saudi nationals only, per current GOSI rules) — see
    /// CompanySettings.GosiEnabled/GosiEmployeeRatePercent etc. Defaults to false so no employee is charged
    /// GOSI by accident; must be explicitly set per employee.</summary>
    public bool IsSaudiNational { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code of the employee's nationality (e.g. "SA", "EG", "GB"). Null for
    /// records created before nationality was captured. IsSaudiNational is kept in sync (true only for "SA") so
    /// GOSI logic keeps working unchanged.</summary>
    public string? Nationality { get; set; }

    /// <summary>Annual paid-leave entitlement used to compute the employee's remaining leave balance
    /// (entitlement minus approved Leave-type requests already taken this calendar year). Defaults to 21,
    /// the Saudi Labor Law Article 109 minimum — adjust per employee/contract as needed.</summary>
    public int AnnualLeaveDaysPerYear { get; set; } = 21;

    // ---- UK payroll (used only when the company's country is the United Kingdom) ----
    /// <summary>National Insurance number, e.g. "QQ123456C".</summary>
    public string? UkNationalInsuranceNumber { get; set; }
    /// <summary>PAYE tax code, e.g. "1257L". Blank means the standard code 1257L.</summary>
    public string? UkTaxCode { get; set; }
    /// <summary>National Insurance category letter (A for most employees). Blank means A.</summary>
    public string? UkNiCategory { get; set; }
    public UkStudentLoanPlan UkStudentLoanPlan { get; set; }
    public bool UkPostgraduateLoan { get; set; }
    /// <summary>Enrolled in the workplace pension; contributions are taken on qualifying earnings.</summary>
    public bool UkPensionEnrolled { get; set; }
    public decimal UkPensionEmployeePercent { get; set; } = 5m;
    public decimal UkPensionEmployerPercent { get; set; } = 3m;

    public ICollection<EmployeeSalaryComponent> SalaryComponents { get; set; } = new List<EmployeeSalaryComponent>();
}
