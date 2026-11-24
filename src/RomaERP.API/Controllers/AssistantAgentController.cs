using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Assistant.Agent;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;

namespace RomaERP.API.Controllers;

/// <summary>The assistant. HR staff may use it too, but each tool and each action checks the roles it is open to, so they only reach the HR parts.</summary>
[ApiController]
[Authorize(Roles = "Admin,Accountant,HR")]
[Route("api/assistant-agent")]
public class AssistantAgentController : ControllerBase
{
    private readonly IAssistantAgentService _agent;
    private readonly IAssistantActionService _actions;
    private readonly IAssistantChatStore _chats;
    private readonly IAssistantAttentionService _attention;

    public AssistantAgentController(IAssistantAgentService agent, IAssistantActionService actions, IAssistantChatStore chats, IAssistantAttentionService attention)
    {
        _agent = agent; _actions = actions; _chats = chats; _attention = attention;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AgentChatResponseDto>> Chat(AgentChatRequestDto request, CancellationToken ct) => Ok(await _agent.ChatAsync(request, ct));

    /// <summary>Books a draft the person reviewed. The service checks the person's role for each kind of action.</summary>
    [HttpPost("execute")]
    public async Task<ActionResult<ExecuteActionResultDto>> Execute(ExecuteActionRequestDto request, CancellationToken ct) => Ok(await _actions.ExecuteAsync(request, ct));

    /// <summary>A photo of a supplier invoice (mode vendor_invoice) or a receipt (mode receipt) turned into a draft. Nothing is booked.</summary>
    [HttpPost("attach")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ActionProposalDto>> Attach(IFormFile file, [FromForm] string mode, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ValidationAppException("الملف فاضي.");
        if (mode is not ("vendor_invoice" or "receipt")) throw new ValidationAppException("اختار نوع الملف.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return Ok(await _actions.ProposeFromImageAsync(ms.ToArray(), file.ContentType.ToLowerInvariant(), mode, ct));
    }

    [HttpGet("attention")]
    public async Task<ActionResult<List<AttentionItemDto>>> Attention(CancellationToken ct) => Ok(await _attention.GetAsync(ct));

    [HttpGet("sessions")]
    public async Task<ActionResult<List<ChatSessionSummaryDto>>> Sessions(CancellationToken ct) => Ok(await _chats.ListAsync(ct));

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<ChatSessionDto>> Session(Guid id, CancellationToken ct) => Ok(await _chats.GetAsync(id, ct));

    [HttpPut("sessions")]
    public async Task<ActionResult<ChatSessionSummaryDto>> Save(SaveChatRequestDto request, CancellationToken ct) => Ok(await _chats.SaveAsync(request, ct));

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken ct) { await _chats.DeleteAsync(id, ct); return NoContent(); }
}
