using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Integration;

namespace RomaERP.API.Controllers;

public class IntegrationSendRequest
{
    public string Type { get; set; } = string.Empty;
    public string ExternalRef { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
}

public class IntegrationBatchRequest
{
    public List<IntegrationSendRequest> Items { get; set; } = new();
}

public class IntegrationBatchEntry
{
    public string ExternalRef { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public string? Status { get; set; }
    public bool Duplicate { get; set; }
    public string? Error { get; set; }
}

/// <summary>The door for outside programs (a bookkeeping bot, another system). Not a logged-in user: the caller proves itself with an
/// API key (X-Api-Key) plus the company code (X-Company-Code, resolved by the tenant middleware like every other call). Nothing sent
/// here touches the books: it lands in the inbox and waits for a person's approval.</summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("integration")]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    private const int MaxBatch = 100;
    private readonly IIntegrationApiKeyService _keys;
    private readonly IIntegrationInboxService _inbox;

    public IntegrationController(IIntegrationApiKeyService keys, IIntegrationInboxService inbox)
    {
        _keys = keys;
        _inbox = inbox;
    }

    private async Task<string?> KeyNameAsync(CancellationToken ct)
        => Request.Headers.TryGetValue("X-Api-Key", out var key) ? await _keys.ValidateAsync(key.ToString(), ct) : null;

    private ActionResult Unauthorized401() => Unauthorized(new { error = "مفتاح الـ API مفقود أو غير صحيح أو ملغي (X-Api-Key)." });

    /// <summary>Checks the key and company: returns ok when both are right.</summary>
    [HttpGet("ping")]
    public async Task<ActionResult> Ping(CancellationToken ct)
        => await KeyNameAsync(ct) is { } name ? Ok(new { ok = true, key = name }) : Unauthorized401();

    [HttpPost("items")]
    public async Task<ActionResult> Send(IntegrationSendRequest request, CancellationToken ct)
    {
        var name = await KeyNameAsync(ct);
        if (name is null) return Unauthorized401();
        var result = await _inbox.ReceiveAsync(name, request.Type, request.ExternalRef, request.Data, ct);
        return result.Duplicate ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Up to 100 items in one call. Each item succeeds or fails on its own; the answer lists them in the same order.</summary>
    [HttpPost("items/batch")]
    public async Task<ActionResult> SendBatch(IntegrationBatchRequest request, CancellationToken ct)
    {
        var name = await KeyNameAsync(ct);
        if (name is null) return Unauthorized401();
        if (request.Items.Count == 0 || request.Items.Count > MaxBatch)
            return BadRequest(new { error = $"ابعت من 1 لـ {MaxBatch} عنصر في المرة." });

        var results = new List<IntegrationBatchEntry>();
        foreach (var item in request.Items)
        {
            var entry = new IntegrationBatchEntry { ExternalRef = item.ExternalRef, Type = item.Type };
            try
            {
                var r = await _inbox.ReceiveAsync(name, item.Type, item.ExternalRef, item.Data, ct);
                entry.Id = r.Id; entry.Status = r.Status; entry.Duplicate = r.Duplicate;
            }
            catch (ValidationAppException ex) { entry.Error = ex.Message; }
            results.Add(entry);
        }
        return Ok(new { accepted = results.Count(r => r.Error is null), rejected = results.Count(r => r.Error is not null), results });
    }

    /// <summary>The bot can ask what happened to something it sent (Pending, Approved, Rejected or Failed, and why).</summary>
    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult> Status(Guid id, CancellationToken ct)
    {
        if (await KeyNameAsync(ct) is null) return Unauthorized401();
        var item = await _inbox.GetAsync(id, ct);
        return Ok(new { item.Id, item.Type, item.ExternalRef, item.Status, item.ErrorMessage, item.ReviewNote, item.ReviewedAtUtc, item.ResultEntityId });
    }
}
