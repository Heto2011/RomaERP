using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Billing;
using RomaERP.Infrastructure.Persistence.Central;
using Xunit;

namespace RomaERP.UnitTests;

public class InvoiceReminderServiceTests
{
    private class FakeEmail : IEmailSender
    {
        public bool IsConfigured { get; set; } = true;
        public List<(string To, string Subject)> Sent { get; } = new();
        public Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((to, subject));
            return Task.FromResult(new EmailSendResult(true, null));
        }
    }

    private static CentralDbContext NewCentral()
        => new(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SubscriptionInvoice AddInvoice(CentralDbContext central, DateTime due, string? email = "owner@example.com", bool demo = false)
    {
        var tenant = new Tenant { CompanyCode = "t1", CompanyNameAr = "شركة", CompanyNameEn = "Co", Country = Country.Egypt, DatabaseName = "db", IsDemo = demo, ContactEmail = email };
        var invoice = new SubscriptionInvoice { TenantId = tenant.Id, SubscriptionId = Guid.NewGuid(), PlanCode = "mini", PlanNameAr = "ميني", TotalAmount = 499, Currency = "EGP", DueDateUtc = due };
        central.Tenants.Add(tenant);
        central.SubscriptionInvoices.Add(invoice);
        central.SaveChanges();
        return invoice;
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    [InlineData(40, 3)]
    public void StageFor_FollowsTheDaysLate(int daysLate, int expected)
    {
        var now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, InvoiceReminderService.StageFor(now.AddDays(-daysLate), now));
    }

    [Fact]
    public async Task EachStageIsEmailedOnce_AndNeverRepeats()
    {
        var central = NewCentral();
        var now = DateTime.UtcNow;
        AddInvoice(central, now.AddDays(-1));
        var email = new FakeEmail();
        var service = new InvoiceReminderService(central, email, NullLogger<InvoiceReminderService>.Instance);

        Assert.Equal(1, await service.SendDueRemindersAsync(now));
        Assert.Equal(0, await service.SendDueRemindersAsync(now.AddHours(6)));      // same stage: nothing more
        Assert.Equal(1, await service.SendDueRemindersAsync(now.AddDays(7)));       // a week later: the warning
        Assert.Equal(2, email.Sent.Count);
        Assert.Equal(2, (await central.SubscriptionInvoices.SingleAsync()).ReminderStage);
    }

    [Fact]
    public async Task NoEmail_DemoCompany_OrAnUnconfiguredSender_SendsNothing()
    {
        var central = NewCentral();
        var now = DateTime.UtcNow;
        AddInvoice(central, now.AddDays(-2), email: null);
        var email = new FakeEmail();
        Assert.Equal(0, await new InvoiceReminderService(central, email, NullLogger<InvoiceReminderService>.Instance).SendDueRemindersAsync(now));

        var central2 = NewCentral();
        AddInvoice(central2, now.AddDays(-2), demo: true);
        Assert.Equal(0, await new InvoiceReminderService(central2, email, NullLogger<InvoiceReminderService>.Instance).SendDueRemindersAsync(now));

        var central3 = NewCentral();
        AddInvoice(central3, now.AddDays(-2));
        Assert.Equal(0, await new InvoiceReminderService(central3, new FakeEmail { IsConfigured = false }, NullLogger<InvoiceReminderService>.Instance).SendDueRemindersAsync(now));
        Assert.Empty(email.Sent);
    }
}
