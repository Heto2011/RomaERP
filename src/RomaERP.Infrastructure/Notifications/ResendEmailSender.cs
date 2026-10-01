using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Notifications;

/// <summary>Sends email through Resend (resend.com). Needs <c>Resend:ApiKey</c> and a sender address on a domain
/// verified in the Resend dashboard (<c>Resend:From</c>, default no-reply@romagroup.app).</summary>
public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _from;

    public ResendEmailSender(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.resend.com/");
        _apiKey = configuration["Resend:ApiKey"];
        _from = configuration["Resend:From"] is { Length: > 0 } from ? from : "Roma Group <no-reply@romagroup.app>";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured) return new EmailSendResult(false, "Email provider is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new { from = _from, to = new[] { to }, subject, html = htmlBody })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey!.Trim());

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return new EmailSendResult(true, null);
            var body = await response.Content.ReadAsStringAsync(ct);
            return new EmailSendResult(false, $"Resend returned {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new EmailSendResult(false, ex.Message);
        }
    }
}
