using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Assistant;
using RomaERP.Infrastructure.Persistence;
using RomaERP.Infrastructure.Persistence.Central;
using Xunit;

namespace RomaERP.UnitTests;

public class AiUsageLimiterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ApplicationDbContext CreateAppContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private class FakeUserLanguage : IUserLanguage
    {
        public bool PrefersArabic { get; init; }
    }

    private class FakeTenantContext : ITenantContext
    {
        public Guid TenantId { get; init; }
        public string CompanyCode { get; init; } = "test";
        public string ConnectionString { get; init; } = string.Empty;
        public Country Country { get; init; }
        public ProductScope ProductScope { get; init; } = ProductScope.Full;
        public bool IsResolved { get; init; } = true;
    }

    /// <summary>Seeds a central DB (in-memory) with one tenant subscribed to the given plan code, so the
    /// limiter can resolve that tenant's plan-based daily cap the same way it does against the real database.</summary>
    private static CentralDbContext CreateCentralContext(string? planCode)
    {
        var options = new DbContextOptionsBuilder<CentralDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var central = new CentralDbContext(options);

        if (planCode is not null)
        {
            var plan = new SubscriptionPlan { Code = planCode, NameAr = planCode, NameEn = planCode };
            central.SubscriptionPlans.Add(plan);
            central.Subscriptions.Add(new Subscription
            {
                TenantId = TenantId,
                PlanId = plan.Id,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            });
            central.SaveChanges();
        }

        return central;
    }

    private static AiUsageLimiter CreateLimiter(ApplicationDbContext ctx, string? planCode, bool arabic = false)
        => new(ctx, CreateCentralContext(planCode), new FakeTenantContext { TenantId = TenantId }, new FakeUserLanguage { PrefersArabic = arabic });

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_UnderLimit_IncrementsAndDoesNotThrow()
    {
        var ctx = CreateAppContext();
        // Essential's BusinessQa cap is 20/day.
        var limiter = CreateLimiter(ctx, "essential");

        await limiter.EnsureWithinDailyLimitAsync("BusinessQa");
        await limiter.EnsureWithinDailyLimitAsync("BusinessQa");

        var counter = await ctx.AiUsageCounters.SingleAsync(c => c.FeatureKey == "BusinessQa");
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_AtLimit_ThrowsAndDoesNotIncrementFurther()
    {
        var ctx = CreateAppContext();
        // Essential's ExpenseCapture cap is 80/day — cheapest way to hit a cap quickly in a test is to
        // pre-seed the counter right at the limit rather than looping 80 real calls.
        ctx.AiUsageCounters.Add(new RomaERP.Domain.Assistant.AiUsageCounter
        {
            FeatureKey = "ExpenseCapture", UsageDate = DateTime.UtcNow.Date, Count = 80
        });
        await ctx.SaveChangesAsync();
        var limiter = CreateLimiter(ctx, "essential");

        await Assert.ThrowsAsync<ValidationAppException>(
            () => limiter.EnsureWithinDailyLimitAsync("ExpenseCapture"));

        var counter = await ctx.AiUsageCounters.SingleAsync(c => c.FeatureKey == "ExpenseCapture");
        Assert.Equal(80, counter.Count); // the throwing call never incremented
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_DifferentFeatureKeys_TrackedSeparately()
    {
        var ctx = CreateAppContext();
        var limiter = CreateLimiter(ctx, "business");

        await limiter.EnsureWithinDailyLimitAsync("BusinessQa");
        await limiter.EnsureWithinDailyLimitAsync("ExpenseCapture");

        // Neither should throw — each feature has its own counter, even on the same day.
        var counters = await ctx.AiUsageCounters.ToListAsync();
        Assert.Equal(2, counters.Count);
        Assert.All(counters, c => Assert.Equal(1, c.Count));
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_DifferentDay_StartsAFreshCounter()
    {
        var ctx = CreateAppContext();
        var limiter = CreateLimiter(ctx, "professional");
        await limiter.EnsureWithinDailyLimitAsync("BusinessQa");

        // Simulate "yesterday" by moving the existing row's date back, as if it were written a day earlier.
        var yesterdayCounter = await ctx.AiUsageCounters.SingleAsync();
        yesterdayCounter.UsageDate = yesterdayCounter.UsageDate.AddDays(-1);
        await ctx.SaveChangesAsync();

        // Today's call should succeed against a brand-new counter, not the (already-at-limit) old one.
        await limiter.EnsureWithinDailyLimitAsync("BusinessQa");

        var counters = await ctx.AiUsageCounters.ToListAsync();
        Assert.Equal(2, counters.Count);
    }

    [Theory]
    [InlineData("essential", 20, 80)]
    [InlineData("business", 50, 200)]
    [InlineData("professional", 100, 400)]
    [InlineData("enterprise", 300, 1000)]
    public async Task EnsureWithinDailyLimitAsync_UsesTheCapForTheTenantsPlan(
        string planCode, int businessQaLimit, int expenseCaptureLimit)
    {
        var qaCtx = CreateAppContext();
        var qaLimiter = CreateLimiter(qaCtx, planCode);
        for (var i = 0; i < businessQaLimit; i++)
            await qaLimiter.EnsureWithinDailyLimitAsync("BusinessQa");
        await Assert.ThrowsAsync<ValidationAppException>(() => qaLimiter.EnsureWithinDailyLimitAsync("BusinessQa"));

        var expenseCtx = CreateAppContext();
        var expenseLimiter = CreateLimiter(expenseCtx, planCode);
        for (var i = 0; i < expenseCaptureLimit; i++)
            await expenseLimiter.EnsureWithinDailyLimitAsync("ExpenseCapture");
        await Assert.ThrowsAsync<ValidationAppException>(() => expenseLimiter.EnsureWithinDailyLimitAsync("ExpenseCapture"));
    }

    [Fact]
    public async Task EnsureWithinDailyLimitAsync_NoSubscriptionRow_FallsBackToEssentialsConservativeCap()
    {
        var ctx = CreateAppContext();
        // No plan seeded at all (e.g. a brand-new tenant whose subscription row hasn't been created yet)
        // must fall back to the most conservative cap, not the most generous one.
        var limiter = CreateLimiter(ctx, planCode: null);

        for (var i = 0; i < 20; i++)
            await limiter.EnsureWithinDailyLimitAsync("BusinessQa");

        await Assert.ThrowsAsync<ValidationAppException>(() => limiter.EnsureWithinDailyLimitAsync("BusinessQa"));
    }

    [Theory]
    [InlineData(false, "You've reached today's limit")]
    [InlineData(true, "وصلتوا للحد الأقصى")]
    public async Task EnsureWithinDailyLimitAsync_OverTheCap_MessageFollowsTheUsersLanguage(bool arabic, string expectedStart)
    {
        var ctx = CreateAppContext();
        ctx.AiUsageCounters.Add(new RomaERP.Domain.Assistant.AiUsageCounter
        {
            FeatureKey = "BusinessQa", UsageDate = DateTime.UtcNow.Date, Count = 20
        });
        await ctx.SaveChangesAsync();
        var limiter = CreateLimiter(ctx, "essential", arabic);

        var ex = await Assert.ThrowsAsync<ValidationAppException>(() => limiter.EnsureWithinDailyLimitAsync("BusinessQa"));

        Assert.StartsWith(expectedStart, ex.Message);
    }

    [Fact]
    public void AiLanguage_EnglishGetsADirective_ArabicPromptsAreLeftUntouched()
    {
        Assert.Contains("English", AiLanguage.ReplyDirective(prefersArabic: false));
        Assert.Equal(string.Empty, AiLanguage.ReplyDirective(prefersArabic: true));
    }

    [Theory]
    [InlineData("people", 100)]
    [InlineData("essential", 150)]
    [InlineData("business", 350)]
    [InlineData("professional", 650)]
    public async Task MonthlyCap_BlocksOnceBothFeaturesTogetherReachTheLimit(string plan, int monthlyCap)
    {
        var ctx = CreateAppContext();
        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        // Spread across earlier days of this month and both features, each day well under its daily limit.
        ctx.AiUsageCounters.Add(new RomaERP.Domain.Assistant.AiUsageCounter { FeatureKey = "BusinessQa", UsageDate = firstOfMonth, Count = monthlyCap / 2 });
        ctx.AiUsageCounters.Add(new RomaERP.Domain.Assistant.AiUsageCounter { FeatureKey = "ExpenseCapture", UsageDate = firstOfMonth, Count = monthlyCap - monthlyCap / 2 });
        await ctx.SaveChangesAsync();
        var limiter = CreateLimiter(ctx, plan);

        await Assert.ThrowsAsync<ValidationAppException>(() => limiter.EnsureWithinDailyLimitAsync("BusinessQa"));
    }

    [Fact]
    public async Task MonthlyCap_ResetsWithTheNewMonth()
    {
        var ctx = CreateAppContext();
        var lastMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddDays(-1);
        ctx.AiUsageCounters.Add(new RomaERP.Domain.Assistant.AiUsageCounter { FeatureKey = "BusinessQa", UsageDate = lastMonth, Count = 5000 });
        await ctx.SaveChangesAsync();

        await CreateLimiter(ctx, "essential").EnsureWithinDailyLimitAsync("BusinessQa"); // does not throw
    }

    [Fact]
    public async Task Enterprise_HasNoMonthlyCap_OnlyTheDailyLimits()
    {
        var ctx = CreateAppContext();
        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        ctx.AiUsageCounters.Add(new RomaERP.Domain.Assistant.AiUsageCounter { FeatureKey = "ExpenseCapture", UsageDate = firstOfMonth, Count = 5000 });
        await ctx.SaveChangesAsync();

        await CreateLimiter(ctx, "enterprise").EnsureWithinDailyLimitAsync("BusinessQa"); // does not throw
    }
}
