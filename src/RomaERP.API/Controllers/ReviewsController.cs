using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Tenancy;
using RomaERP.Infrastructure.Persistence.Central;
using RomaERP.Infrastructure.Tenancy;

namespace RomaERP.API.Controllers;

public record SubmitReviewRequest(int Rating, string? Comment, string? DisplayName, bool AllowPublish, bool ShowCompany);
public record ReviewDecisionRequest(string Status, string? Note);

/// <summary>Inside the system: ask a signed-in person for their opinion, and say whether it is time to.</summary>
[ApiController]
[Authorize]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly CustomerReviewService _service;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserService _user;
    private readonly CentralDbContext _central;

    public FeedbackController(CustomerReviewService service, ITenantContext tenant, ICurrentUserService user, CentralDbContext central)
    {
        _service = service; _tenant = tenant; _user = user; _central = central;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ReviewPromptStatus>> Status(CancellationToken ct)
        => Ok(await _service.GetStatusAsync(_tenant.TenantId, _user.UserId, DateTime.UtcNow, ct));

    [HttpPost]
    public async Task<IActionResult> Submit(SubmitReviewRequest request, CancellationToken ct)
    {
        var companyName = await _central.Tenants.AsNoTracking().Where(t => t.Id == _tenant.TenantId).Select(t => t.CompanyNameEn).FirstOrDefaultAsync(ct);
        var product = _tenant.ProductScope switch { ProductScope.PeopleOnly => "HR", ProductScope.PosOnly => "POS", ProductScope.BooksOnly => "Books", _ => "ERP" };
        await _service.SubmitAsync(new SubmitReview(_tenant.TenantId, _tenant.CompanyCode, companyName, _user.UserId, _user.UserName ?? "user",
            request.DisplayName, request.Rating, request.Comment, request.AllowPublish, request.ShowCompany, product), DateTime.UtcNow, ct);
        return NoContent();
    }
}

/// <summary>The website: only approved reviews whose author agreed to publish them. Anonymous, not tenant-scoped.</summary>
[ApiController]
[Route("api/marketing/reviews")]
public class PublicReviewsController : ControllerBase
{
    private readonly CustomerReviewService _service;
    public PublicReviewsController(CustomerReviewService service) => _service = service;

    [HttpGet]
    [EnableRateLimiting("marketing-pageview")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<PublicReviews>> Get(CancellationToken ct) => Ok(await _service.GetPublicAsync(ct));
}

/// <summary>The platform owner's console: read what customers wrote and decide what may appear on the website.</summary>
[ApiController]
[Route("api/system/reviews")]
[EnableRateLimiting("system-key")]
public class ReviewsAdminController : ControllerBase
{
    private readonly CustomerReviewService _service;
    private readonly IConfiguration _configuration;
    public ReviewsAdminController(CustomerReviewService service, IConfiguration configuration) { _service = service; _configuration = configuration; }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        var denied = CheckSystemKey();
        if (denied is not null) return denied;
        ReviewStatus? filter = Enum.TryParse<ReviewStatus>(status, true, out var s) ? s : null;
        return Ok(await _service.ListAsync(filter, ct));
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<ActionResult> Decide(Guid id, ReviewDecisionRequest request, CancellationToken ct)
    {
        var denied = CheckSystemKey();
        if (denied is not null) return denied;
        if (!Enum.TryParse<ReviewStatus>(request.Status, true, out var status) || status == ReviewStatus.Pending) return BadRequest(new { error = "القرار لازم يكون Approved أو Rejected." });
        await _service.DecideAsync(id, status, request.Note, ct);
        return NoContent();
    }

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey)) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "System:ProvisioningKey غير مضبوط." });
        if (!Request.Headers.TryGetValue("X-System-Key", out var provided)
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString())), SHA256.HashData(Encoding.UTF8.GetBytes(systemKey))))
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });
        return null;
    }
}
