namespace RomaERP.Application.Common.Interfaces;

public record TransferUserRequest(
    string SourceCompanyCode,
    string TargetCompanyCode,
    string Email,
    string NewPassword,
    bool DeactivateInSource = true);

public record TransferUserResult(string Email, string FullName, List<string> Roles, string TargetCompanyCode);

/// <summary>Recreates a user's basic account (email, name, roles) in a different tenant's database and
/// optionally deactivates the original — the closest thing to "moving" a user between companies, since
/// each tenant's database is fully isolated from every other's (a real requirement, not a limitation to
/// work around). Anything tied to their old company — attendance history, leave requests, payroll runs —
/// deliberately stays behind; it belongs to that company, not to the person.</summary>
public interface IUserTransferService
{
    Task<TransferUserResult> TransferAsync(TransferUserRequest request, CancellationToken ct = default);
}
