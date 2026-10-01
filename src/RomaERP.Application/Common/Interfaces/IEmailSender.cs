namespace RomaERP.Application.Common.Interfaces;

public record EmailSendResult(bool Success, string? FailureReason);

/// <summary>Sends one transactional email (password reset, welcome, payment reminder). When no provider key is
/// configured <see cref="IsConfigured"/> is false and nothing is sent — callers must not pretend it was.</summary>
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
