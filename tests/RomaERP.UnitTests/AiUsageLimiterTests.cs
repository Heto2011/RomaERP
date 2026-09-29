using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Infrastructure.Assistant;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class AiUsageLimiterTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_UnderLimit_IncrementsAndDoesNotThrow()
    {
        var ctx = CreateContext();
        var limiter = new AiUsageLimiter(ctx);

        await limiter.EnsureWithinDailyLimitAsync("BusinessQa", dailyLimit: 3);
        await limiter.EnsureWithinDailyLimitAsync("BusinessQa", dailyLimit: 3);

        var counter = await ctx.AiUsageCounters.SingleAsync(c => c.FeatureKey == "BusinessQa");
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_AtLimit_ThrowsAndDoesNotIncrementFurther()
    {
        var ctx = CreateContext();
        var limiter = new AiUsageLimiter(ctx);

        await limiter.EnsureWithinDailyLimitAsync("ExpenseCapture", dailyLimit: 2);
        await limiter.EnsureWithinDailyLimitAsync("ExpenseCapture", dailyLimit: 2);

        await Assert.ThrowsAsync<ValidationAppException>(
            () => limiter.EnsureWithinDailyLimitAsync("ExpenseCapture", dailyLimit: 2));

        var counter = await ctx.AiUsageCounters.SingleAsync(c => c.FeatureKey == "ExpenseCapture");
        Assert.Equal(2, counter.Count); // the throwing call never incremented
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_DifferentFeatureKeys_TrackedSeparately()
    {
        var ctx = CreateContext();
        var limiter = new AiUsageLimiter(ctx);

        await limiter.EnsureWithinDailyLimitAsync("BusinessQa", dailyLimit: 1);
        await limiter.EnsureWithinDailyLimitAsync("ExpenseCapture", dailyLimit: 1);

        // Neither should throw — each feature has its own counter, even on the same day.
        var counters = await ctx.AiUsageCounters.ToListAsync();
        Assert.Equal(2, counters.Count);
        Assert.All(counters, c => Assert.Equal(1, c.Count));
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_DifferentDay_StartsAFreshCounter()
    {
        var ctx = CreateContext();
        var limiter = new AiUsageLimiter(ctx);
        await limiter.EnsureWithinDailyLimitAsync("BusinessQa", dailyLimit: 1);

        // Simulate "yesterday" by moving the existing row's date back, as if it were written a day earlier.
        var yesterdayCounter = await ctx.AiUsageCounters.SingleAsync();
        yesterdayCounter.UsageDate = yesterdayCounter.UsageDate.AddDays(-1);
        await ctx.SaveChangesAsync();

        // Today's call should succeed against a brand-new counter, not the (already-at-limit) old one.
        await limiter.EnsureWithinDailyLimitAsync("BusinessQa", dailyLimit: 1);

        var counters = await ctx.AiUsageCounters.ToListAsync();
        Assert.Equal(2, counters.Count);
    }
}
