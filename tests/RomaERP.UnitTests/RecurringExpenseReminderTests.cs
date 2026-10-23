using Microsoft.Extensions.Logging.Abstractions;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Notifications.DTOs;
using RomaERP.Application.Notifications.Services;
using RomaERP.Domain.Assistant;
using Xunit;

namespace RomaERP.UnitTests;

public class RecurringExpenseReminderTests
{
    private class FakeWhatsApp : IWhatsAppNotificationService
    {
        public List<string> Sent { get; } = new();
        public bool Fail { get; set; }
        public Task<WhatsAppStatusDto> GetStatusAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WhatsAppStatusDto> SaveCredentialAsync(SaveWhatsAppCredentialDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WhatsAppSendResultDto> SendTestMessageAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WhatsAppSendResultDto> SendAlertsDigestAsync(bool skipIfNothingSignificant, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WhatsAppSendResultDto> SendTextAsync(string text, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("boom");
            Sent.Add(text);
            return Task.FromResult(new WhatsAppSendResultDto { Success = true });
        }
    }

    private class FakeEmail : IEmailSender
    {
        public bool IsConfigured { get; set; } = true;
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((to, subject, htmlBody));
            return Task.FromResult(new EmailSendResult(true, null));
        }
    }

    private class FakeAdmins : IAdminEmailProvider
    {
        public Task<List<AdminContact>> GetAdminContactsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<AdminContact> { new("boss@acme.test", "Boss <b>") });
    }

    private static async Task<(RecurringExpenseReminderService svc, FakeWhatsApp wa, FakeEmail mail)> BuildAsync(bool withItem = true)
    {
        var (ctx, _, expense, _, _) = await ExpenseAssistantServiceTests.SeedAsync();
        var recurring = new RecurringExpenseService(ctx);
        if (withItem) await recurring.SaveAsync(null, new RecurringExpenseDto { Description = "إيجار", AccountId = expense.Id, Amount = 5000, DayOfMonth = 3, PaymentMethod = PaymentMethod.Cash });
        var wa = new FakeWhatsApp();
        var mail = new FakeEmail();
        return (new RecurringExpenseReminderService(recurring, ctx, wa, mail, new FakeAdmins(), NullLogger<RecurringExpenseReminderService>.Instance), wa, mail);
    }

    [Fact]
    public async Task NoticeGoesOutOnce_WhenSomethingWasPrepared_ToWhatsAppAndEveryAdminEmail()
    {
        var (svc, wa, mail) = await BuildAsync();

        var first = await svc.RunAsync(new DateTime(2026, 10, 3), true, "https://romagroup.app/", default);
        Assert.Equal(1, first.Prepared);
        Assert.Equal(1, first.PendingApproval);
        Assert.True(first.WhatsAppSent);
        Assert.Equal(1, first.EmailsSent);
        Assert.Contains("https://romagroup.app/assistant/approvals", wa.Sent.Single());
        Assert.Contains("1", wa.Sent.Single());
        Assert.Equal("boss@acme.test", mail.Sent.Single().To);
        Assert.Contains("Boss &lt;b&gt;", mail.Sent.Single().Html);   // the name is HTML-encoded
        Assert.DoesNotContain("Boss <b>", mail.Sent.Single().Html);

        var again = await svc.RunAsync(new DateTime(2026, 10, 3), true, "https://romagroup.app", default);
        Assert.Equal(0, again.Prepared);
        Assert.Single(wa.Sent);
        Assert.Single(mail.Sent);
    }

    [Fact]
    public async Task NothingDue_SendsNothing()
    {
        var (svc, wa, mail) = await BuildAsync();
        var outcome = await svc.RunAsync(new DateTime(2026, 10, 2), true, "https://x", default);
        Assert.Equal(0, outcome.Prepared);
        Assert.Empty(wa.Sent);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task WhatsAppFailure_DoesNotStopEmail_AndNoProviderMeansNoEmail()
    {
        var (svc, wa, mail) = await BuildAsync();
        wa.Fail = true;
        var outcome = await svc.RunAsync(new DateTime(2026, 10, 3), false, "https://x", default);
        Assert.False(outcome.WhatsAppSent);
        Assert.Equal(1, outcome.EmailsSent);
        Assert.Contains("fixed monthly", mail.Sent.Single().Html);

        var (svc2, _, mail2) = await BuildAsync();
        mail2.IsConfigured = false;
        Assert.Equal(0, (await svc2.RunAsync(new DateTime(2026, 10, 3), true, "https://x", default)).EmailsSent);
        Assert.Empty(mail2.Sent);
    }
}
