namespace RomaERP.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    /// <summary>The roles of the signed-in person (empty when unknown).</summary>
    IReadOnlyList<string> Roles => Array.Empty<string>();
}
