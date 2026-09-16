using RomaERP.Domain.Common;

namespace RomaERP.Domain.Audit;

/// <summary>One recorded login attempt — success or failure — with the client's IP address, so an admin
/// can see who actually used the system, when, and from where (and spot repeated failed attempts).</summary>
public class LoginHistory : BaseEntity
{
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "unknown";
    public bool Success { get; set; }
    public string Method { get; set; } = "Password";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
