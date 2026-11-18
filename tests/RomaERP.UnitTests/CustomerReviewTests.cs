using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

public class CustomerReviewTests
{
    private static CentralDbContext NewCentral() => new(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SubmitReview Review(Guid tenant, string? user = "u1", int rating = 5, string? comment = "ممتاز", bool publish = true, bool company = false)
        => new(tenant, "acme", "Acme Foods", user, "Mona Ali", null, rating, comment, publish, company, "ERP");

    [Theory]
    [InlineData(2026, 11, 26, true, "2026-11")]   // last five days of November
    [InlineData(2026, 11, 30, true, "2026-11")]
    [InlineData(2026, 12, 2, true, "2026-11")]    // first days of the next month still answer November
    [InlineData(2026, 11, 15, false, "2026-11")]  // mid-month: not asked
    [InlineData(2026, 2, 24, true, "2026-02")]    // February has 28 days
    public void TheAskingWindow_IsTheEndOfTheMonthAndTheFirstDaysOfTheNext(int y, int m, int d, bool inWindow, string key)
        => Assert.Equal((inWindow, key), CustomerReviewService.PromptWindow(new DateTime(y, m, d)));

    [Fact]
    public async Task OneReviewPerPersonPerMonth_ASecondOneReplacesTheFirst_AndIsLookedAtAgain()
    {
        var central = NewCentral();
        var svc = new CustomerReviewService(central);
        var tenant = Guid.NewGuid();
        var day = new DateTime(2026, 11, 28);
        await svc.SubmitAsync(Review(tenant), day);
        var first = await central.CustomerReviews.SingleAsync();
        await svc.DecideAsync(first.Id, ReviewStatus.Approved, null);

        await svc.SubmitAsync(Review(tenant, rating: 3, comment: "تمام"), day);
        var only = await central.CustomerReviews.SingleAsync();
        Assert.Equal(3, only.Rating);
        Assert.Equal(ReviewStatus.Pending, only.Status);    // a changed review is not shown until approved again
        Assert.True((await svc.GetStatusAsync(tenant, "u1", day)).Answered);
        Assert.False((await svc.GetStatusAsync(tenant, "u2", day)).Answered);
    }

    [Fact]
    public async Task OnlyApprovedReviews_ThatTheCustomerAgreedToPublish_AppearOnTheWebsite()
    {
        var central = NewCentral();
        var svc = new CustomerReviewService(central);
        var day = new DateTime(2026, 11, 28);
        await svc.SubmitAsync(Review(Guid.NewGuid(), "a", 5, "ممتاز", publish: true, company: true), day);
        await svc.SubmitAsync(Review(Guid.NewGuid(), "b", 4, "كويس", publish: false), day);       // did not agree to publish
        await svc.SubmitAsync(Review(Guid.NewGuid(), "c", 1, "وحش", publish: true), day);          // will be rejected
        var rows = await central.CustomerReviews.OrderBy(r => r.UserId).ToListAsync();
        await svc.DecideAsync(rows[0].Id, ReviewStatus.Approved, null);
        await svc.DecideAsync(rows[2].Id, ReviewStatus.Rejected, "مسيء");
        await Assert.ThrowsAsync<ValidationAppException>(() => svc.DecideAsync(rows[1].Id, ReviewStatus.Approved, null));   // no consent to publish

        var pub = await svc.GetPublicAsync();
        var item = Assert.Single(pub.Items);
        Assert.Equal("Mona", item.DisplayName);          // the first name by default, not the full name
        Assert.Equal("Acme Foods", item.CompanyName);    // only because the customer agreed to show it
        Assert.Equal(1, pub.Count);
        Assert.Equal(5, pub.Average);
    }

    [Fact]
    public async Task TheCompanyName_IsHiddenUnlessTheCustomerAllowedIt()
    {
        var central = NewCentral();
        var svc = new CustomerReviewService(central);
        await svc.SubmitAsync(Review(Guid.NewGuid(), company: false), new DateTime(2026, 11, 28));
        await svc.DecideAsync((await central.CustomerReviews.SingleAsync()).Id, ReviewStatus.Approved, null);
        Assert.Null((await svc.GetPublicAsync()).Items.Single().CompanyName);
    }

    [Theory]
    [InlineData(0, "x", true)]
    [InlineData(6, "x", true)]
    [InlineData(5, null, true)]    // asking to publish needs a comment
    public async Task BadReviews_AreRefused(int rating, string? comment, bool publish)
        => await Assert.ThrowsAsync<ValidationAppException>(() => new CustomerReviewService(NewCentral()).SubmitAsync(Review(Guid.NewGuid(), rating: rating, comment: comment, publish: publish), new DateTime(2026, 11, 28)));
}
