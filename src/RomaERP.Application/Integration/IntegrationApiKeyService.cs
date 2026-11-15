using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Integration;

namespace RomaERP.Application.Integration;

public interface IIntegrationApiKeyService
{
    Task<List<ApiKeyDto>> ListAsync(CancellationToken ct = default);
    Task<CreatedApiKeyDto> CreateAsync(string name, CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Returns the key's name when the key is valid and active, otherwise null. Marks the key as used.</summary>
    Task<string?> ValidateAsync(string? key, CancellationToken ct = default);
}

public class IntegrationApiKeyService : IIntegrationApiKeyService
{
    private const string Prefix = "rk_";
    private readonly IApplicationDbContext _context;

    public IntegrationApiKeyService(IApplicationDbContext context) => _context = context;

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public async Task<List<ApiKeyDto>> ListAsync(CancellationToken ct = default)
        => (await _context.IntegrationApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAtUtc).ToListAsync(ct)).Select(Map).ToList();

    public async Task<CreatedApiKeyDto> CreateAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationAppException("اكتب اسم للمفتاح (مثلًا: البوت المحاسبي).");
        var active = await _context.IntegrationApiKeys.CountAsync(k => k.IsActive, ct);
        if (active >= 10) throw new ValidationAppException("وصلت لـ 10 مفاتيح شغالة. ألغِ مفتاح قبل ما تعمل واحد جديد.");

        var bytes = RandomNumberGenerator.GetBytes(30);
        var key = Prefix + Convert.ToBase64String(bytes).Replace('+', 'A').Replace('/', 'B').TrimEnd('=');
        var entity = new IntegrationApiKey { Name = name.Trim(), KeyHash = Hash(key), KeyPrefix = key[..8] };
        _context.IntegrationApiKeys.Add(entity);
        await _context.SaveChangesAsync(ct);
        var dto = Map(entity);
        return new CreatedApiKeyDto
        {
            Id = dto.Id, Name = dto.Name, KeyPrefix = dto.KeyPrefix, IsActive = true, CreatedAtUtc = dto.CreatedAtUtc, Key = key
        };
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _context.IntegrationApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct) ?? throw new NotFoundException(nameof(IntegrationApiKey), id);
        key.IsActive = false;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<string?> ValidateAsync(string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(Prefix, StringComparison.Ordinal) || key.Length > 100) return null;
        var hash = Hash(key.Trim());
        var entity = await _context.IntegrationApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive, ct);
        if (entity is null) return null;
        // Touch at most once a minute so a busy bot does not write on every call.
        if (entity.LastUsedAtUtc is null || entity.LastUsedAtUtc < DateTime.UtcNow.AddMinutes(-1))
        {
            entity.LastUsedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        return entity.Name;
    }

    private static ApiKeyDto Map(IntegrationApiKey k) => new()
    {
        Id = k.Id, Name = k.Name, KeyPrefix = k.KeyPrefix, IsActive = k.IsActive, CreatedAtUtc = k.CreatedAtUtc, LastUsedAtUtc = k.LastUsedAtUtc
    };
}
