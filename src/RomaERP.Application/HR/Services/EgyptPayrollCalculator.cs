namespace RomaERP.Application.HR.Services;

public record EgyptPayrollResult(decimal EmployeeInsurance, decimal EmployerInsurance, decimal IncomeTax);

/// <summary>Monthly Egyptian payroll for 2026: social insurance on the insurable wage (employee 11%, employer 18.75%, with the
/// 2026 minimum and maximum insurable wage) and salary income tax on the annualised pay after employee insurance and the personal
/// exemption. The figures are the published 2026 rates but several details are sources-disagree items (the personal exemption, extra
/// funds such as the Martyrs' Fund, the high-income phase-out above 600,000 a year), so they must be confirmed by an Egyptian
/// accountant. Health-insurance contributions are not included.</summary>
public static class EgyptPayrollCalculator
{
    public const string Year = "2026";

    private const decimal MinInsurableWage = 2_700m;
    private const decimal MaxInsurableWage = 16_700m;
    private const decimal EmployeeRate = 0.11m;
    private const decimal EmployerRate = 0.1875m;
    private const decimal PersonalExemption = 20_000m;

    // Annual income brackets: upper limit of each band and its rate. The last band is open-ended.
    private static readonly (decimal UpTo, decimal Rate)[] Brackets =
    {
        (40_000m, 0m), (55_000m, 0.10m), (70_000m, 0.15m), (200_000m, 0.20m), (400_000m, 0.225m), (1_200_000m, 0.25m), (decimal.MaxValue, 0.275m),
    };

    /// <summary>Whether the employee is covered by Egyptian social insurance: Egyptian nationals (or nationality not recorded) are, other
    /// nationalities generally are not unless an agreement applies.</summary>
    public static bool IsInsured(string? nationality)
        => string.IsNullOrWhiteSpace(nationality) || string.Equals(nationality.Trim(), "EG", StringComparison.OrdinalIgnoreCase);

    public static EgyptPayrollResult Calculate(decimal basicSalary, decimal grossPay, bool insured)
    {
        decimal employee = 0, employer = 0;
        if (insured)
        {
            var wage = Math.Clamp(basicSalary, MinInsurableWage, MaxInsurableWage);
            if (basicSalary > 0)
            {
                employee = Round(wage * EmployeeRate);
                employer = Round(wage * EmployerRate);
            }
        }

        var annualTaxable = Math.Max(0, (Math.Max(0, grossPay) - employee) * 12m - PersonalExemption);
        return new EgyptPayrollResult(employee, employer, Round(AnnualTax(annualTaxable) / 12m));
    }

    private static decimal AnnualTax(decimal annualTaxable)
    {
        decimal tax = 0, lower = 0;
        foreach (var (upTo, rate) in Brackets)
        {
            if (annualTaxable <= lower) break;
            tax += (Math.Min(annualTaxable, upTo) - lower) * rate;
            lower = upTo;
        }
        return tax;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
