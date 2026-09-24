using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.API.Contracts;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;

namespace RomaERP.API.Controllers;

/// <summary>Anonymous page-view logging for the public marketing pages (pricing.html etc.) — no cookies,
/// no visitor identity, just enough to see traffic volume and where it came from. Not tenant-scoped (see
/// TenantResolutionMiddleware's exempt prefixes), since a marketing-page visitor has no tenant at all.
/// The write endpoint is open to anyone (that's the point); reading the results is system-key gated like
/// every other cross-tenant admin view.</summary>
[ApiController]
[Route("api/marketing")]
public class MarketingController : ControllerBase
{
    private readonly CentralDbContext _central;
    private readonly IConfiguration _configuration;

    public MarketingController(CentralDbContext central, IConfiguration configuration)
    {
        _central = central;
        _configuration = configuration;
    }

    [HttpPost("pageview")]
    [EnableRateLimiting("marketing-pageview")]
    public async Task<IActionResult> RecordPageView(RecordPageViewRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Path)) return NoContent();

        _central.MarketingPageViews.Add(new MarketingPageView
        {
            ViewedAtUtc = DateTime.UtcNow,
            Path = Truncate(request.Path, 200)!,
            Referrer = Truncate(request.Referrer, 300),
            UserAgent = Truncate(Request.Headers.UserAgent.ToString(), 300),
        });
        await _central.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("pageviews")]
    public async Task<ActionResult<List<MarketingPageViewDto>>> GetPageViews(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var views = await _central.MarketingPageViews
            .OrderByDescending(v => v.ViewedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        return Ok(views.Select(v => new MarketingPageViewDto(v.Id, v.ViewedAtUtc, v.Path, v.Referrer, v.UserAgent)).ToList());
    }

    [HttpGet("pageviews/stats")]
    public async Task<ActionResult<MarketingPageViewStatsDto>> GetStats(CancellationToken ct)
    {
        var keyCheck = CheckSystemKey();
        if (keyCheck is not null) return keyCheck;

        var now = DateTime.UtcNow;
        var all = await _central.MarketingPageViews.ToListAsync(ct);

        var topPaths = all.GroupBy(v => v.Path)
            .Select(g => new CountByLabelDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).Take(10).ToList();

        var topReferrers = all.GroupBy(v => string.IsNullOrWhiteSpace(v.Referrer) ? "(مباشر)" : v.Referrer!)
            .Select(g => new CountByLabelDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).Take(10).ToList();

        return Ok(new MarketingPageViewStatsDto(
            TotalViews: all.Count,
            Last7Days: all.Count(v => v.ViewedAtUtc >= now.AddDays(-7)),
            Last30Days: all.Count(v => v.ViewedAtUtc >= now.AddDays(-30)),
            TopPaths: topPaths,
            TopReferrers: topReferrers));
    }

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) ? value : (value.Length <= maxLength ? value : value[..maxLength]);

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "لوحة الإحصائيات مش مفعّلة — لازم تضيف System:ProvisioningKey في الإعدادات." });

        if (!Request.Headers.TryGetValue("X-System-Key", out var providedKey) || !FixedTimeEquals(providedKey.ToString(), systemKey))
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });

        return null;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
