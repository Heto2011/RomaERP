namespace RomaERP.Application.Common.Interfaces;

/// <summary>"Forgot my password" by email, for the tenant resolved on the current request.</summary>
public interface IPasswordRecoveryService
{
    /// <summary>Emails a one-time reset link if the address belongs to an active user. Never reveals whether it did.</summary>
    Task RequestResetAsync(string email, CancellationToken ct = default);

    /// <summary>Sets the new password if the token from the email is valid. Returns an error message, or null on success.</summary>
    Task<string?> ResetAsync(string email, string token, string newPassword, CancellationToken ct = default);
}
