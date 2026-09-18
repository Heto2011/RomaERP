namespace RomaERP.Application.Common.Interfaces;

public record ResetSystemUserPasswordRequest(string CompanyCode, string Email, string NewPassword);

/// <summary>Last-resort password reset for when nobody can log in to a tenant to use the normal
/// Admin-resets-any-user's-password feature (e.g. the only Admin account is locked out). Bypasses tenant
/// auth entirely, so it's gated by the same system key as the rest of SystemController, not a JWT.</summary>
public interface ISystemPasswordResetService
{
    Task ResetPasswordAsync(ResetSystemUserPasswordRequest request, CancellationToken ct = default);
}
