using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Infrastructure.Support;
using Xunit;

namespace RomaERP.UnitTests;

public class SupportNotifierTests
{
    private class Mail : IEmailSender
    {
        public bool IsConfigured { get; set; } = true;
        public bool Throw { get; set; }
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("down");
            Sent.Add((to, subject, htmlBody)); return Task.FromResult(new EmailSendResult(true, null));
        }
    }
    private static IConfiguration Cfg(string? to = null) => new ConfigurationBuilder().AddInMemoryCollection(to is null ? new Dictionary<string, string?>() : new() { ["Support:NotifyEmail"] = to }).Build();

    [Fact]
    public async Task NewTicket_EmailsTheOwner_WithTheCompany_AndEscapesTheText()
    {
        var m = new Mail(); var n = new SupportNotifier(m, Cfg("me@example.com"));
        await n.NotifyOwnerAsync(7, "ACME", "منى", "مشكلة", "<script>x</script> مش بيطبع", true, false);
        var s = Assert.Single(m.Sent);
        Assert.Equal("me@example.com", s.To);
        Assert.Contains("#7", s.Subject); Assert.Contains("ACME", s.Body);
        Assert.DoesNotContain("<script>", s.Body);
    }

    [Fact]
    public async Task OwnerAddress_DefaultsToSupport_AndNothingIsSentWhenEmailIsNotConfigured_OrFails()
    {
        var m = new Mail(); await new SupportNotifier(m, Cfg()).NotifyOwnerAsync(1, "C", "n", "s", "m", false, false);
        Assert.Equal(SupportNotifier.DefaultOwnerAddress, m.Sent[0].To);
        var off = new Mail { IsConfigured = false }; await new SupportNotifier(off, Cfg()).NotifyOwnerAsync(1, "C", "n", "s", "m", true, false);
        Assert.Empty(off.Sent);
        var bad = new Mail { Throw = true };
        await new SupportNotifier(bad, Cfg()).NotifyOwnerAsync(1, "C", "n", "s", "m", true, false);
    }
}
