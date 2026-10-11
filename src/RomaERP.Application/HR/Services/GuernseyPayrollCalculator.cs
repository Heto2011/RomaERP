namespace RomaERP.Application.HR.Services;

public record GuernseyPayrollResult(decimal IncomeTax, decimal EmployeeSocial, decimal EmployerSocial);

/// <summary>Monthly Guernsey payroll: a flat income tax on annualised pay above the personal allowance, and employee and employer
/// social insurance on pay up to a monthly ceiling. Every rate, the allowance and the ceiling come from the company's payroll
/// settings so they can follow Guernsey's yearly changes. The tax is an estimate: in Guernsey the Revenue Service tells the employer
/// what to deduct for each employee (employer tax instalments), so a payroll line can be edited when that instruction differs.</summary>
public static class GuernseyPayrollCalculator
{
    public static GuernseyPayrollResult Calculate(decimal grossPay, decimal taxRatePercent, decimal personalAllowanceAnnual,
        decimal employeeSocialPercent, decimal employerSocialPercent, decimal socialMonthlyUpperLimit)
    {
        var gross = Math.Max(0, grossPay);
        var annualTaxable = Math.Max(0, gross * 12m - Math.Max(0, personalAllowanceAnnual));
        var tax = Round(annualTaxable * taxRatePercent / 100m / 12m);

        var socialBase = socialMonthlyUpperLimit > 0 ? Math.Min(gross, socialMonthlyUpperLimit) : gross;
        return new GuernseyPayrollResult(tax, Round(socialBase * employeeSocialPercent / 100m), Round(socialBase * employerSocialPercent / 100m));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
