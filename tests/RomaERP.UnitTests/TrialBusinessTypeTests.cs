using RomaERP.API.Controllers;
using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class TrialBusinessTypeTests
{
    [Fact]
    public void OneToThreeKnownActivities_AreKept()
    {
        var picked = BusinessType.Restaurant | BusinessType.Contracting;
        Assert.Equal(picked, TrialController.SanitizeBusinessTypes(picked));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1 | 2 | 4 | 8)]      // four activities say nothing about the business
    [InlineData(64)]                 // an unknown bit only
    public void NothingOrTooMany_BecomesNone(int raw)
        => Assert.Equal(BusinessType.None, TrialController.SanitizeBusinessTypes((BusinessType)raw));

    [Fact]
    public void UnknownBits_AreDropped()
        => Assert.Equal(BusinessType.Retail, TrialController.SanitizeBusinessTypes((BusinessType)(2 | 128)));
}
