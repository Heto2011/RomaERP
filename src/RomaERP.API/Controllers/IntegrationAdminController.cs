using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.Integration;
using RomaERP.Domain.Integration;

namespace RomaERP.API.Controllers;

public record CreateApiKeyRequest(string Name);
public record RejectInboxRequest(string? Reason);

/// <summary>The company's own screen for the bot connection: API keys (Admin only) and the inbox of what the bot sent.</summary>
[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/integration-admin")]
public class IntegrationAdminController : ControllerBase
{
    private readonly IIntegrationApiKeyService _keys;
    private readonly IIntegrationInboxService _inbox;
    private readonly ICurrentUserService _user;

    public IntegrationAdminController(IIntegrationApiKeyService keys, IIntegrationInboxService inbox, ICurrentUserService user)
    {
        _keys = keys;
        _inbox = inbox;
        _user = user;
    }

    private string Reviewer => _user.UserName ?? _user.UserId ?? "—";

    [HttpGet("keys")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ApiKeyDto>>> Keys(CancellationToken ct) => Ok(await _keys.ListAsync(ct));

    [HttpPost("keys")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreatedApiKeyDto>> CreateKey(CreateApiKeyRequest request, CancellationToken ct)
        => Ok(await _keys.CreateAsync(request.Name, ct));

    [HttpDelete("keys/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RevokeKey(Guid id, CancellationToken ct)
    {
        await _keys.RevokeAsync(id, ct);
        return NoContent();
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<List<InboxItemDto>>> Inbox([FromQuery] IntegrationItemStatus? status, CancellationToken ct)
        => Ok(await _inbox.ListAsync(status, ct));

    [HttpGet("inbox/{id:guid}")]
    public async Task<ActionResult<InboxItemDto>> Item(Guid id, CancellationToken ct) => Ok(await _inbox.GetAsync(id, ct));

    [HttpPost("inbox/{id:guid}/approve")]
    public async Task<ActionResult<InboxItemDto>> Approve(Guid id, CancellationToken ct) => Ok(await _inbox.ApproveAsync(id, Reviewer, ct));

    [HttpPost("inbox/{id:guid}/reject")]
    public async Task<ActionResult<InboxItemDto>> Reject(Guid id, RejectInboxRequest request, CancellationToken ct)
        => Ok(await _inbox.RejectAsync(id, request.Reason, Reviewer, ct));

    [HttpPost("inbox/approve-all")]
    public async Task<ActionResult<ApproveAllResult>> ApproveAll(CancellationToken ct) => Ok(await _inbox.ApproveAllPendingAsync(Reviewer, ct));
}
