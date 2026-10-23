using RomaERP.Infrastructure.Assistant;
using Xunit;

namespace RomaERP.UnitTests;

public class AiUsageOverviewTests
{
    [Theory]
    [InlineData("essential", 100, 33, "ok")]
    [InlineData("essential", 240, 80, "warning")]
    [InlineData("essential", 300, 100, "over")]
    [InlineData("mini", 150, 150, "over")]
    public void LevelFollowsShareOfMonthlyCap(string plan, int used, int expectedPercent, string expectedLevel)
    {
        var row = AiUsageOverviewService.Summarize("acme", "Acme", plan, false, 1, 2, used);
        Assert.Equal(expectedPercent, row.PercentOfLimit);
        Assert.Equal(expectedLevel, row.Level);
        Assert.Equal(used * 0.01m, row.EstimatedCostUsd);
    }

    [Theory]
    [InlineData("enterprise")]
    [InlineData(null)]
    public void NoMonthlyCap_IsUnlimited(string? plan)
    {
        var row = AiUsageOverviewService.Summarize("acme", "Acme", plan, true, 0, 0, 5000);
        Assert.Null(row.MonthlyLimit);
        Assert.Null(row.PercentOfLimit);
        Assert.Equal("unlimited", row.Level);
    }
}
