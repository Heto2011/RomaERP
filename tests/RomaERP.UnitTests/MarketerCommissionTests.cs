using Microsoft.EntityFrameworkCore;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Billing;
using RomaERP.Infrastructure.Persistence.Central;
using Xunit;

namespace RomaERP.UnitTests;

public class MarketerCommissionTests
{
    private static readonly DateTime First = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 0.20)]     // the first payment itself
    [InlineData(11, 0.20)]    // still in the first year
    [InlineData(12, 0.10)]    // exactly a year later: second-year rate
    [InlineData(23, 0.10)]
    [InlineData(24, 0.00)]    // two years after the first payment: no commission
    [InlineData(40, 0.00)]
    public void Rate_FollowsTheTwoYearWindow(int monthsAfterFirst, double expected)
        => Assert.Equal((decimal)expected, MarketerCommissionPolicy.RateFor(First, First.AddMonths(monthsAfterFirst)));

    private static CentralDbContext NewCentral()
        => new(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SubscriptionInvoice Paid(Guid tenantId, DateTime at, decimal amount) => new()
    {
        TenantId = tenantId, SubscriptionId = Guid.NewGuid(), PlanCode = "essential", PlanNameAr = "x",
        TotalAmount = amount, Currency = "USD", Status = SubscriptionInvoiceStatus.Paid, PaidAtUtc = at,
        PeriodStart = at, PeriodEnd = at.AddMonths(1), DueDateUtc = at
    };

    [Fact]
    public async Task Sync_CreatesOneCommissionPerPaidInvoice_AndIsIdempotent()
    {
        using var central = NewCentral();
        var service = new MarketerCommissionService(central);
        var tenant = Guid.NewGuid();
        var m = await service.CreateAsync("Ali", "ali-01", null, null);
        await service.RecordReferralAsync(tenant, " ALI-01 ");

        central.SubscriptionInvoices.Add(Paid(tenant, First, 100m));
        central.SubscriptionInvoices.Add(Paid(tenant, First.AddMonths(13), 100m)); // second year
        central.SubscriptionInvoices.Add(Paid(tenant, First.AddMonths(25), 100m)); // after two years: nothing
        central.SubscriptionInvoices.Add(Paid(tenant, First.AddMonths(1), 0m));    // free: nothing
        await central.SaveChangesAsync();

        await service.SyncAsync();
        await service.SyncAsync(); // second run must not double count

        var rows = await central.MarketerCommissions.OrderBy(c => c.PaymentPaidAtUtc).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(20m, rows[0].Amount);
        Assert.Equal(10m, rows[1].Amount);
        Assert.All(rows, r => Assert.Equal(m.Id, r.MarketerId));
    }

    [Fact]
    public async Task PayOut_OnlyPaysCommissionsPastTheHoldPeriod_AndVoidedOnesAreSkipped()
    {
        using var central = NewCentral();
        var service = new MarketerCommissionService(central);
        var tenant = Guid.NewGuid();
        var m = await service.CreateAsync("Sara", "SARA", null, null);
        await service.RecordReferralAsync(tenant, "sara");

        central.SubscriptionInvoices.Add(Paid(tenant, DateTime.UtcNow.AddDays(-60), 50m)); // payable (20% = 10)
        central.SubscriptionInvoices.Add(Paid(tenant, DateTime.UtcNow.AddDays(-5), 50m));  // still in the hold period
        central.SubscriptionInvoices.Add(Paid(tenant, DateTime.UtcNow.AddDays(-45), 50m)); // payable, then voided
        await central.SaveChangesAsync();

        await service.SyncAsync();
        var voided = await central.MarketerCommissions.FirstAsync(c => c.PaymentPaidAtUtc < DateTime.UtcNow.AddDays(-40) && c.PaymentPaidAtUtc > DateTime.UtcNow.AddDays(-50));
        await service.VoidAsync(voided.Id, "refund");

        var paidOut = await service.PayOutAsync(m.Id, "bank transfer");
        Assert.Single(paidOut);
        Assert.Equal(10m, paidOut[0].PaidOut);

        var summary = (await service.ListAsync()).Single();
        var totals = summary.Totals.Single();
        Assert.Equal(10m, totals.PaidOut);
        Assert.Equal(10m, totals.Waiting); // the 5-day-old payment: 20% of 50, still inside the hold period
    }

    [Fact]
    public async Task EmptyCode_IsGeneratedAutomatically_AndUnique()
    {
        using var central = NewCentral();
        var service = new MarketerCommissionService(central);
        var a = await service.CreateAsync("A", null, null, null);
        var b = await service.CreateAsync("B", "  ", null, null);
        Assert.Equal(6, a.Code.Length);
        Assert.Equal(6, b.Code.Length);
        Assert.NotEqual(a.Code, b.Code);
        Assert.Matches("^[A-Z2-9]{6}$", a.Code);
    }

    [Fact]
    public async Task UnknownOrInactiveCode_IsIgnored_AndDuplicateCodesAreRejected()
    {
        using var central = NewCentral();
        var service = new MarketerCommissionService(central);
        var tenant = Guid.NewGuid();
        await service.RecordReferralAsync(tenant, "NOPE");
        Assert.Empty(central.MarketerReferrals);

        var m = await service.CreateAsync("X", "CODE1", null, null);
        await service.SetActiveAsync(m.Id, false);
        await service.RecordReferralAsync(tenant, "CODE1");
        Assert.Empty(central.MarketerReferrals);

        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync("Y", "code1", null, null));
        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync("Z", "a b", null, null));
    }
}
