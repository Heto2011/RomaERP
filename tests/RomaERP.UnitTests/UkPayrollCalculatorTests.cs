using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.Services.Uk;
using RomaERP.Domain.HR;
using Xunit;

namespace RomaERP.UnitTests;

public class UkPayrollCalculatorTests
{
    private static UkPayrollInput Input(decimal gross, string code = "1257L", string ni = "A", int month = 1, UkYearToDate? ytd = null,
        UkStudentLoanPlan loan = UkStudentLoanPlan.None, bool postgrad = false, bool pension = false)
        => new(gross, code, ni, loan, postgrad, pension, 5m, 3m, month, ytd ?? UkYearToDate.None);

    [Fact]
    public void StandardEmployee_OnThreeThousandAMonth_MatchesTheHmrcTables()
    {
        var r = UkPayrollCalculator.Calculate(Input(3000m));

        Assert.Equal(390.20m, r.IncomeTax);      // (3000 - 1048.25 free pay, rounded down to 1951) x 20%
        Assert.Equal(156.16m, r.EmployeeNi);     // (3000 - 1048) x 8%
        Assert.Equal(387.45m, r.EmployerNi);     // (3000 - 417) x 15%
        Assert.Equal(3000m, r.TaxablePay);
    }

    [Fact]
    public void CumulativeBasis_SecondMonthOfTheSameSalary_TaxesTheSameAgain()
    {
        var first = UkPayrollCalculator.Calculate(Input(3000m));
        var second = UkPayrollCalculator.Calculate(Input(3000m, month: 2, ytd: new UkYearToDate(first.TaxablePay, first.IncomeTax)));

        Assert.Equal(390.40m, second.IncomeTax); // free pay to date 2,096.50 -> taxable 3,903 -> 780.60 due, 390.20 already paid
    }

    [Fact]
    public void CumulativeBasis_RefundsTaxWhenAMonthIsUnpaid()
    {
        var first = UkPayrollCalculator.Calculate(Input(3000m));
        var second = UkPayrollCalculator.Calculate(Input(0m, month: 2, ytd: new UkYearToDate(first.TaxablePay, first.IncomeTax)));

        Assert.Equal(-209.60m, second.IncomeTax);
    }

    [Fact]
    public void MonthOneCode_IgnoresEarlierPay()
    {
        var r = UkPayrollCalculator.Calculate(Input(3000m, code: "1257L M1", month: 5, ytd: new UkYearToDate(12000m, 1500m)));

        Assert.Equal(390.20m, r.IncomeTax);
    }

    [Fact]
    public void HigherEarner_PaysHigherRateAndTheTwoPercentNi()
    {
        var r = UkPayrollCalculator.Calculate(Input(6000m));

        Assert.Equal(1352.00m, r.IncomeTax);     // 3,142 at 20% + 1,809 at 40%
        Assert.Equal(287.50m, r.EmployeeNi);     // 8% up to 4,189 + 2% above
        Assert.Equal(837.45m, r.EmployerNi);
    }

    [Fact]
    public void BelowThresholds_PaysNothing()
    {
        var r = UkPayrollCalculator.Calculate(Input(400m));

        Assert.Equal(0m, r.IncomeTax);
        Assert.Equal(0m, r.EmployeeNi);
        Assert.Equal(0m, r.EmployerNi);
    }

    [Fact]
    public void Pension_UsesQualifyingEarnings_AndReducesTaxablePay()
    {
        var r = UkPayrollCalculator.Calculate(Input(3000m, pension: true));

        Assert.Equal(124.00m, r.PensionEmployee);   // 5% of (3000 - 520)
        Assert.Equal(74.40m, r.PensionEmployer);    // 3% of the same
        Assert.Equal(2876m, r.TaxablePay);
        Assert.Equal(365.40m, r.IncomeTax);
        Assert.Equal(156.16m, r.EmployeeNi);        // National Insurance is still on the full gross
    }

    [Fact]
    public void StudentAndPostgraduateLoans_AreRoundedDownToWholePounds()
    {
        var r = UkPayrollCalculator.Calculate(Input(3000m, loan: UkStudentLoanPlan.Plan2, postgrad: true));

        Assert.Equal(49m, r.StudentLoan);           // 9% of (3000 - 2448) = 49.68
        Assert.Equal(75m, r.PostgraduateLoan);      // 6% of (3000 - 1750)
    }

    [Theory]
    [InlineData("H")]
    [InlineData("M")]
    public void ApprenticeAndUnder21Categories_PayNoEmployerNiBelowTheUpperThreshold(string category)
    {
        var r = UkPayrollCalculator.Calculate(Input(3000m, ni: category));

        Assert.Equal(0m, r.EmployerNi);
        Assert.Equal(156.16m, r.EmployeeNi);
    }

    [Fact]
    public void PensionAgeCategory_PaysNoEmployeeNi()
    {
        var r = UkPayrollCalculator.Calculate(Input(3000m, ni: "C"));

        Assert.Equal(0m, r.EmployeeNi);
        Assert.Equal(387.45m, r.EmployerNi);
    }

    [Theory]
    [InlineData("BR", 3000, 600.00)]
    [InlineData("D0", 3000, 1200.00)]
    [InlineData("NT", 3000, 0.00)]
    [InlineData("0T", 3000, 600.00)]   // no allowance: 20% of 3,000
    public void SpecialTaxCodes(string code, double gross, double expectedTax)
        => Assert.Equal((decimal)expectedTax, UkPayrollCalculator.Calculate(Input((decimal)gross, code: code)).IncomeTax);

    [Fact]
    public void KCode_AddsToTaxablePay()
    {
        var r = UkPayrollCalculator.Calculate(Input(2000m, code: "K475"));

        Assert.Equal(479.20m, r.IncomeTax);  // allowance -4,759/12 = -396.58, so (2000 + 396.58) floored to 2,396 x 20%
    }

    [Theory]
    [InlineData("S1257L")]
    [InlineData("12ABC")]
    [InlineData("")]
    public void ScottishAndInvalidCodes_AreRejected(string code)
        => Assert.Throws<ValidationAppException>(() => UkPayrollCalculator.Calculate(Input(3000m, code: code)));

    [Fact]
    public void WelshCode_UsesTheSameRatesAsEngland()
        => Assert.Equal(390.20m, UkPayrollCalculator.Calculate(Input(3000m, code: "C1257L")).IncomeTax);

    [Theory]
    [InlineData("2026-04-28", 1)]
    [InlineData("2026-05-05", 1)]
    [InlineData("2026-05-06", 2)]
    [InlineData("2027-03-28", 12)]
    [InlineData("2027-04-05", 12)]
    public void TaxMonth_FollowsTheSixthToFifthCalendar(string date, int month)
    {
        var (_, m) = UkPayrollCalculator.TaxPeriodOf(DateTime.Parse(date));
        Assert.Equal(month, m);
    }

    [Fact]
    public void TaxYear_StartsOnSixApril()
    {
        Assert.Equal(new DateTime(2026, 4, 6), UkPayrollCalculator.TaxPeriodOf(new DateTime(2026, 9, 1)).YearStart);
        Assert.Equal(new DateTime(2025, 4, 6), UkPayrollCalculator.TaxPeriodOf(new DateTime(2026, 4, 5)).YearStart);
    }
}
