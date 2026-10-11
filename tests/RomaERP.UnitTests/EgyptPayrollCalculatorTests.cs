using RomaERP.Application.HR.Services;
using Xunit;

namespace RomaERP.UnitTests;

public class EgyptPayrollCalculatorTests
{
    [Fact]
    public void InsuranceIsChargedOnTheInsurableWage_CappedAtTheCeiling()
    {
        var r = EgyptPayrollCalculator.Calculate(basicSalary: 30000m, grossPay: 30000m, insured: true);

        Assert.Equal(1837.00m, r.EmployeeInsurance);   // 11% of 16,700
        Assert.Equal(3131.25m, r.EmployerInsurance);   // 18.75% of 16,700
    }

    [Fact]
    public void InsuranceHasAMinimumWage()
    {
        var r = EgyptPayrollCalculator.Calculate(basicSalary: 1500m, grossPay: 1500m, insured: true);

        Assert.Equal(297.00m, r.EmployeeInsurance);    // 11% of the 2,700 minimum
    }

    [Fact]
    public void NotInsured_PaysNoInsurance()
    {
        var r = EgyptPayrollCalculator.Calculate(basicSalary: 10000m, grossPay: 10000m, insured: false);

        Assert.Equal(0m, r.EmployeeInsurance);
        Assert.Equal(0m, r.EmployerInsurance);
    }

    [Fact]
    public void LowSalary_PaysNoIncomeTax()
    {
        // 5,000 a month: (5,000 - 550 insurance) x 12 - 20,000 = 33,400, inside the 40,000 exempt band
        Assert.Equal(0m, EgyptPayrollCalculator.Calculate(5000m, 5000m, true).IncomeTax);
    }

    [Fact]
    public void IncomeTax_AppliesTheBracketsToTheAnnualisedPay()
    {
        var r = EgyptPayrollCalculator.Calculate(basicSalary: 20000m, grossPay: 20000m, insured: true);

        // monthly pay after insurance 18,163 -> annual 217,956 - 20,000 = 197,956
        // 0 on 40,000; 10% of 15,000 = 1,500; 15% of 15,000 = 2,250; 20% of 127,956 = 25,591.20 -> 29,341.20 / 12
        Assert.Equal(1837.00m, r.EmployeeInsurance);
        Assert.Equal(2445.10m, r.IncomeTax);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("EG", true)]
    [InlineData("eg", true)]
    [InlineData("SA", false)]
    [InlineData("GB", false)]
    public void OnlyEgyptianNationalsAreInsured(string? nationality, bool insured)
        => Assert.Equal(insured, EgyptPayrollCalculator.IsInsured(nationality));
}
