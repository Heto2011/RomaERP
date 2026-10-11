using System.Text.RegularExpressions;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services.Uk;

/// <summary>What one UK pay run costs for one employee, split the way the employer reports it.</summary>
public record UkPayrollResult(
    decimal TaxablePay, decimal IncomeTax, decimal EmployeeNi, decimal EmployerNi,
    decimal StudentLoan, decimal PostgraduateLoan, decimal PensionEmployee, decimal PensionEmployer);

/// <summary>What the employee has earned and paid earlier in the same tax year (cumulative PAYE).</summary>
public record UkYearToDate(decimal TaxablePay, decimal IncomeTax)
{
    public static readonly UkYearToDate None = new(0, 0);
}

public record UkPayrollInput(
    decimal GrossPay, string TaxCode, string NiCategory, UkStudentLoanPlan StudentLoan, bool PostgraduateLoan,
    bool PensionEnrolled, decimal PensionEmployeePercent, decimal PensionEmployerPercent,
    int TaxMonth, UkYearToDate YearToDate);

/// <summary>Monthly UK payroll for the 2026/27 tax year (England, Wales and Northern Ireland rates): income tax by tax code
/// on the cumulative basis, employee and employer National Insurance by category letter, auto-enrolment pension on
/// qualifying earnings (net pay arrangement), and student / postgraduate loan deductions. Rates are the published
/// 2026/27 figures; they are constants here so the next tax year only needs this file updated. Scottish tax codes (S prefix)
/// are not supported yet and are rejected. The output must be checked by a UK payroll accountant before it is relied on.</summary>
public static class UkPayrollCalculator
{
    public const string TaxYear = "2026/27";

    // Income tax: taxable pay bands (annual), applied to pay after the tax-free allowance.
    private const decimal BasicBand = 37_700m;
    private const decimal HigherBandLimit = 125_140m;
    private const decimal BasicRate = 0.20m, HigherRate = 0.40m, AdditionalRate = 0.45m;

    // National Insurance monthly thresholds (HMRC's rounded monthly figures for 2026/27).
    private const decimal PrimaryThreshold = 1_048m;
    private const decimal SecondaryThreshold = 417m;
    private const decimal UpperEarningsLimit = 4_189m;      // also the upper secondary threshold for categories H, M, V and Z
    private const decimal EmployerRate = 0.15m;
    private const decimal MainEmployeeRate = 0.08m, ReducedEmployeeRate = 0.0185m, AboveUelEmployeeRate = 0.02m;

    // Auto-enrolment qualifying earnings band (monthly).
    private const decimal PensionLower = 520m;
    private const decimal PensionUpper = 4_189m;

    // Student loan monthly thresholds (annual threshold / 12, rounded down) and rates.
    private static decimal LoanThreshold(UkStudentLoanPlan plan) => plan switch
    {
        UkStudentLoanPlan.Plan1 => 2_241m,
        UkStudentLoanPlan.Plan2 => 2_448m,
        UkStudentLoanPlan.Plan4 => 2_816m,
        UkStudentLoanPlan.Plan5 => 2_083m,
        _ => decimal.MaxValue,
    };
    private const decimal PostgraduateThreshold = 1_750m;
    private const decimal LoanRate = 0.09m, PostgraduateRate = 0.06m;

    private static readonly Regex AllowanceCode = new(@"^(?:(?<k>K)(?<n>\d{1,5})|(?<n>\d{1,5})[LMNTPY])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly string[] SupportedNiCategories = { "A", "B", "C", "H", "J", "M", "V", "X", "Z" };

    /// <summary>Which UK tax year a pay date falls in, as the date it started (6 April) and the HMRC tax month (1–12).</summary>
    public static (DateTime YearStart, int Month) TaxPeriodOf(DateTime payDate)
    {
        var d = payDate.Date;
        var start = new DateTime(d.Month > 4 || (d.Month == 4 && d.Day >= 6) ? d.Year : d.Year - 1, 4, 6);
        var months = (d.Year - start.Year) * 12 + (d.Month - 4);
        if (d.Day < 6) months--;
        return (start, Math.Clamp(months + 1, 1, 12));
    }

    public static void ValidateTaxCode(string? code)
    {
        _ = ParseTaxCode(code ?? string.Empty);
    }

    public static UkPayrollResult Calculate(UkPayrollInput input)
    {
        var gross = Math.Max(0, input.GrossPay);
        var niCategory = (input.NiCategory ?? "A").Trim().ToUpperInvariant();
        if (!SupportedNiCategories.Contains(niCategory))
            throw new ValidationAppException($"فئة التأمين الوطني {niCategory} غير مدعومة. المدعوم: {string.Join(", ", SupportedNiCategories)}.");

        // Pension on qualifying earnings; employee share comes off pay before tax (net pay arrangement).
        decimal pensionEmployee = 0, pensionEmployer = 0;
        if (input.PensionEnrolled)
        {
            var qualifying = Math.Max(0, Math.Min(gross, PensionUpper) - PensionLower);
            pensionEmployee = Round(qualifying * input.PensionEmployeePercent / 100m);
            pensionEmployer = Round(qualifying * input.PensionEmployerPercent / 100m);
        }

        var taxablePay = gross - pensionEmployee;
        var tax = IncomeTax(taxablePay, input.TaxCode, input.TaxMonth, input.YearToDate);
        var (employeeNi, employerNi) = NationalInsurance(gross, niCategory);

        var studentLoan = 0m;
        if (input.StudentLoan != UkStudentLoanPlan.None && gross > LoanThreshold(input.StudentLoan))
            studentLoan = Math.Floor((gross - LoanThreshold(input.StudentLoan)) * LoanRate);
        var postgraduate = input.PostgraduateLoan && gross > PostgraduateThreshold
            ? Math.Floor((gross - PostgraduateThreshold) * PostgraduateRate)
            : 0m;

        return new UkPayrollResult(Round(taxablePay), tax, employeeNi, employerNi, studentLoan, postgraduate, pensionEmployee, pensionEmployer);
    }

    private static decimal IncomeTax(decimal taxablePayThisMonth, string taxCodeRaw, int month, UkYearToDate ytd)
    {
        var parsed = ParseTaxCode(taxCodeRaw);
        if (parsed.NoTax) return 0;

        // Week 1 / Month 1 codes ignore everything earlier in the year.
        var m = parsed.NonCumulative ? 1 : Math.Clamp(month, 1, 12);
        var priorTaxable = parsed.NonCumulative ? 0 : ytd.TaxablePay;
        var priorTax = parsed.NonCumulative ? 0 : ytd.IncomeTax;
        var taxableToDate = priorTaxable + taxablePayThisMonth;

        decimal dueToDate;
        if (parsed.FlatRate is { } flat)
        {
            dueToDate = Math.Floor(Math.Max(0, taxableToDate)) * flat;
        }
        else
        {
            var freePayToDate = Math.Round(parsed.AnnualAllowance * m / 12m, 2, MidpointRounding.AwayFromZero);
            var pay = Math.Floor(taxableToDate - freePayToDate);
            if (pay <= 0)
            {
                dueToDate = 0;
            }
            else
            {
                var basicLimit = Math.Ceiling(BasicBand * m / 12m);
                var higherLimit = Math.Ceiling(HigherBandLimit * m / 12m);
                dueToDate = Math.Min(pay, basicLimit) * BasicRate
                    + Math.Max(0, Math.Min(pay, higherLimit) - basicLimit) * HigherRate
                    + Math.Max(0, pay - higherLimit) * AdditionalRate;
            }
        }

        return Round(dueToDate - priorTax);
    }

    private static (decimal Employee, decimal Employer) NationalInsurance(decimal gross, string category)
    {
        if (category == "X") return (0, 0);

        // Employee side.
        decimal employee = 0;
        var mainRate = category switch
        {
            "B" => ReducedEmployeeRate,
            "C" => 0m,
            "J" or "Z" => 0m,      // deferred: only the 2% above the primary threshold
            _ => MainEmployeeRate,
        };
        var aboveRate = category == "C" ? 0m : AboveUelEmployeeRate;
        if (category is "J" or "Z")
        {
            if (gross > PrimaryThreshold) employee = (gross - PrimaryThreshold) * AboveUelEmployeeRate;
        }
        else
        {
            if (gross > PrimaryThreshold) employee += (Math.Min(gross, UpperEarningsLimit) - PrimaryThreshold) * mainRate;
            if (gross > UpperEarningsLimit) employee += (gross - UpperEarningsLimit) * aboveRate;
        }

        // Employer side: 15% above the secondary threshold; 0% up to the upper secondary threshold for H, M, V and Z.
        decimal employer;
        if (category is "H" or "M" or "V" or "Z")
            employer = gross > UpperEarningsLimit ? (gross - UpperEarningsLimit) * EmployerRate : 0;
        else
            employer = gross > SecondaryThreshold ? (gross - SecondaryThreshold) * EmployerRate : 0;

        return (Round(employee), Round(employer));
    }

    private record ParsedCode(bool NoTax, decimal? FlatRate, decimal AnnualAllowance, bool NonCumulative);

    private static ParsedCode ParseTaxCode(string raw)
    {
        var code = Regex.Replace((raw ?? string.Empty).Trim().ToUpperInvariant(), @"\s+", "");
        if (code.Length == 0) throw new ValidationAppException("كود الضريبة مطلوب (مثلًا 1257L).");
        if (code.StartsWith('S')) throw new ValidationAppException("أكواد الضريبة الاسكتلندية (S) غير مدعومة بعد.");
        if (code.StartsWith('C')) code = code[1..]; // Welsh codes use the same rates as England

        var nonCumulative = false;
        foreach (var suffix in new[] { "W1", "M1", "X" })
            if (code.EndsWith(suffix, StringComparison.Ordinal) && code.Length > suffix.Length)
            {
                nonCumulative = true;
                code = code[..^suffix.Length];
                break;
            }

        switch (code)
        {
            case "NT": return new(true, null, 0, nonCumulative);
            case "BR": return new(false, BasicRate, 0, nonCumulative);
            case "D0": return new(false, HigherRate, 0, nonCumulative);
            case "D1": return new(false, AdditionalRate, 0, nonCumulative);
            case "0T": return new(false, null, 0, nonCumulative);
        }

        var match = AllowanceCode.Match(code);
        if (!match.Success)
            throw new ValidationAppException($"كود الضريبة «{raw}» غير صالح. أمثلة: 1257L، 0T، BR، D0، D1، NT، K475.");
        var allowance = int.Parse(match.Groups["n"].Value) * 10m + 9m;
        return new(false, null, match.Groups["k"].Success ? -allowance : allowance, nonCumulative);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
