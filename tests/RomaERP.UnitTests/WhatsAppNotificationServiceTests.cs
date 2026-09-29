using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Alerts.DTOs;
using RomaERP.Application.Alerts.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Notifications.DTOs;
using RomaERP.Application.Notifications.Services;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Records every call instead of hitting Meta's real API — lets tests assert whether a send was
/// attempted at all (e.g. the daily digest must skip silently when there's nothing significant to report).</summary>
public class FakeWhatsAppSender : IWhatsAppSender
{
    public int CallCount { get; private set; }
    public string? LastBodyText { get; private set; }
    public WhatsAppSendResult Result { get; set; } = new(true, null);

    public Task<WhatsAppSendResult> SendTemplateMessageAsync(
        string phoneNumberId, string accessToken, string recipientPhoneNumber,
        string templateName, string templateLanguageCode, string bodyText, CancellationToken ct = default)
    {
        CallCount++;
        LastBodyText = bodyText;
        return Task.FromResult(Result);
    }
}

public class FakeAlertsService : IAlertsService
{
    public List<AlertDto> Alerts { get; set; } = new();

    public Task<AlertsReportDto> GetAlertsAsync(CancellationToken ct = default) =>
        Task.FromResult(new AlertsReportDto { GeneratedAt = DateTime.UtcNow, Alerts = Alerts });
}

public class WhatsAppNotificationServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private static SaveWhatsAppCredentialDto ValidCredential() => new()
    {
        PhoneNumberId = "1234567890",
        AccessToken = "secret-token",
        RecipientPhoneNumber = "+201234567890",
        TemplateName = "romaerp_alert",
        TemplateLanguageCode = "ar",
        IsEnabled = true,
    };

    [Fact]
    public async Task GetStatusAsync_WithNoCredential_ReportsNotConfigured()
    {
        var ctx = CreateContext();
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), new FakeWhatsAppSender(), new FakeAlertsService());

        var status = await service.GetStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.False(status.IsEnabled);
    }

    [Fact]
    public async Task SaveCredentialAsync_MissingAccessTokenOnFirstSave_Throws()
    {
        var ctx = CreateContext();
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), new FakeWhatsAppSender(), new FakeAlertsService());
        var dto = ValidCredential();
        dto.AccessToken = null;

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SaveCredentialAsync(dto));
    }

    [Fact]
    public async Task SaveCredentialAsync_ThenGetStatus_ReportsConfigured()
    {
        var ctx = CreateContext();
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), new FakeWhatsAppSender(), new FakeAlertsService());

        var status = await service.SaveCredentialAsync(ValidCredential());

        Assert.True(status.IsConfigured);
        Assert.True(status.IsEnabled);
        Assert.Equal("1234567890", status.PhoneNumberId);
    }

    [Fact]
    public async Task SaveCredentialAsync_WithoutNewToken_KeepsPreviouslySavedToken()
    {
        var ctx = CreateContext();
        var sender = new FakeWhatsAppSender();
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), sender, new FakeAlertsService());
        await service.SaveCredentialAsync(ValidCredential());

        var update = ValidCredential();
        update.AccessToken = null;
        update.RecipientPhoneNumber = "+201111111111";
        await service.SaveCredentialAsync(update);

        var result = await service.SendTestMessageAsync();
        Assert.True(result.Success);
        Assert.Equal(1, sender.CallCount);
    }

    [Fact]
    public async Task SendTestMessageAsync_NotConfigured_ReturnsFailureWithoutCallingSender()
    {
        var ctx = CreateContext();
        var sender = new FakeWhatsAppSender();
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), sender, new FakeAlertsService());

        var result = await service.SendTestMessageAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Equal(0, sender.CallCount);
    }

    [Fact]
    public async Task SendAlertsDigestAsync_SkipIfNothingSignificant_NoAlerts_DoesNotCallSender()
    {
        var ctx = CreateContext();
        var sender = new FakeWhatsAppSender();
        var alerts = new FakeAlertsService { Alerts = new List<AlertDto>() };
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), sender, alerts);
        await service.SaveCredentialAsync(ValidCredential());

        var result = await service.SendAlertsDigestAsync(skipIfNothingSignificant: true);

        Assert.True(result.Success);
        Assert.Equal(0, sender.CallCount);
    }

    [Fact]
    public async Task SendAlertsDigestAsync_OnDemandWithNoAlerts_SendsAllClearMessage()
    {
        var ctx = CreateContext();
        var sender = new FakeWhatsAppSender();
        var alerts = new FakeAlertsService { Alerts = new List<AlertDto>() };
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), sender, alerts);
        await service.SaveCredentialAsync(ValidCredential());

        var result = await service.SendAlertsDigestAsync(skipIfNothingSignificant: false);

        Assert.True(result.Success);
        Assert.Equal(1, sender.CallCount);
    }

    [Fact]
    public async Task SendAlertsDigestAsync_WithSignificantAlerts_SendsDigestAndIgnoresInfoSeverity()
    {
        var ctx = CreateContext();
        var sender = new FakeWhatsAppSender();
        var alerts = new FakeAlertsService
        {
            Alerts = new List<AlertDto>
            {
                new() { Category = "Cash", Severity = AlertSeverity.Critical, Title = "سيولة منخفضة", Detail = "هتخلص خلال 3 أسابيع" },
                new() { Category = "Inventory", Severity = AlertSeverity.Info, Title = "معلومة عادية", Detail = "متجاهلة" },
            }
        };
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), sender, alerts);
        await service.SaveCredentialAsync(ValidCredential());

        var result = await service.SendAlertsDigestAsync(skipIfNothingSignificant: true);

        Assert.True(result.Success);
        Assert.Equal(1, sender.CallCount);
        Assert.Contains("سيولة منخفضة", sender.LastBodyText);
        Assert.DoesNotContain("معلومة عادية", sender.LastBodyText);
    }

    [Fact]
    public async Task SendAlertsDigestAsync_Disabled_DoesNotSend()
    {
        var ctx = CreateContext();
        var sender = new FakeWhatsAppSender();
        var alerts = new FakeAlertsService
        {
            Alerts = new List<AlertDto> { new() { Category = "Cash", Severity = AlertSeverity.Critical, Title = "x", Detail = "y" } }
        };
        var service = new WhatsAppNotificationService(ctx, new PlainTextSecretProtector(), sender, alerts);
        var credential = ValidCredential();
        credential.IsEnabled = false;
        await service.SaveCredentialAsync(credential);

        var result = await service.SendAlertsDigestAsync(skipIfNothingSignificant: true);

        Assert.False(result.Success);
        Assert.Equal(0, sender.CallCount);
    }
}
