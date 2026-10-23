namespace RomaERP.Application.Common.Interfaces;

public record AdminContact(string Email, string Name);

/// <summary>The active Admin users of the current company that have an email address — who gets company-level notices.</summary>
public interface IAdminEmailProvider
{
    Task<List<AdminContact>> GetAdminContactsAsync(CancellationToken ct = default);
}
