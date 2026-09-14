namespace RomaERP.Domain.HR;

public enum Gender
{
    Male = 1,
    Female = 2
}

public enum MaritalStatus
{
    Single = 1,
    Married = 2,
    Divorced = 3,
    Widowed = 4
}

public enum EmploymentStatus
{
    Active = 1,
    OnLeave = 2,
    Terminated = 3
}

public enum SalaryComponentType
{
    Allowance = 1,
    Deduction = 2
}

public enum CalculationType
{
    FixedAmount = 1,
    PercentageOfBasic = 2
}

public enum PayrollRunStatus
{
    Draft = 1,
    Approved = 2,
    Posted = 3
}
