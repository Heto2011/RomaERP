namespace RomaERP.Domain.HR;

/// <summary>Which UK student loan plan an employee repays (deducted by the employer at 9% above the plan's threshold).</summary>
public enum UkStudentLoanPlan
{
    None = 0,
    Plan1 = 1,
    Plan2 = 2,
    Plan4 = 4,
    Plan5 = 5
}
