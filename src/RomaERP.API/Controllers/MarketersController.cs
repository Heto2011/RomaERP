using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RomaERP.Infrastructure.Billing;

namespace RomaERP.API.Controllers;

public record CreateMarketerRequest(string Name, string Code, string? Email, string? Notes);
public record SetMarketerActiveRequest(bool Active);
public record PayOutRequest(string? Note);
public record VoidCommissionRequest(string? Reason);

/// <summary>Platform-owner console for marketers and their commissions. Spans every tenant, so like the other system
/// endpoints it is exempt from tenant resolution and protected by the system key.</summary>
[ApiController]
[Route("api/system/marketers")]
[EnableRateLimiting("system-key")]
public class MarketersController : ControllerBase
{
    private readonly MarketerCommissionService _service;
    private readonly IConfiguration _configuration;

    public MarketersController(MarketerCommissionService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken ct)
        => CheckSystemKey() ?? Ok(await _service.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult> Create(CreateMarketerRequest request, CancellationToken ct)
        => CheckSystemKey() ?? Ok(await _service.CreateAsync(request.Name, request.Code, request.Email, request.Notes, ct));

    [HttpPost("{id:guid}/active")]
    public async Task<ActionResult> SetActive(Guid id, SetMarketerActiveRequest request, CancellationToken ct)
    {
        var check = CheckSystemKey();
        if (check is not null) return check;
        await _service.SetActiveAsync(id, request.Active, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/commissions")]
    public async Task<ActionResult> Commissions(Guid id, CancellationToken ct)
        => CheckSystemKey() ?? Ok(await _service.GetCommissionsAsync(id, ct));

    [HttpPost("{id:guid}/pay-out")]
    public async Task<ActionResult> PayOut(Guid id, PayOutRequest request, CancellationToken ct)
        => CheckSystemKey() ?? Ok(await _service.PayOutAsync(id, request.Note, ct));

    [HttpPost("commissions/{commissionId:guid}/void")]
    public async Task<ActionResult> Void(Guid commissionId, VoidCommissionRequest request, CancellationToken ct)
    {
        var check = CheckSystemKey();
        if (check is not null) return check;
        await _service.VoidAsync(commissionId, request.Reason, ct);
        return NoContent();
    }

    private ActionResult? CheckSystemKey()
    {
        var systemKey = _configuration["System:ProvisioningKey"];
        if (string.IsNullOrEmpty(systemKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "لازم تضيف System:ProvisioningKey في الإعدادات." });
        if (!Request.Headers.TryGetValue("X-System-Key", out var provided) || !FixedTimeEquals(provided.ToString(), systemKey))
            return Unauthorized(new { error = "مفتاح النظام غير صحيح." });
        return null;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
