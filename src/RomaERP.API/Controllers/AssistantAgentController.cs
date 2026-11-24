using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Assistant.Agent;

namespace RomaERP.API.Controllers;

/// <summary>The assistant conversation. HR staff may use it too, but each tool checks the roles it is open to, so they only reach the HR tools.</summary>
[ApiController]
[Authorize(Roles = "Admin,Accountant,HR")]
[Route("api/assistant-agent")]
public class AssistantAgentController : ControllerBase
{
    private readonly IAssistantAgentService _agent;
    public AssistantAgentController(IAssistantAgentService agent) => _agent = agent;

    [HttpPost("chat")]
    public async Task<ActionResult<AgentChatResponseDto>> Chat(AgentChatRequestDto request, CancellationToken ct) => Ok(await _agent.ChatAsync(request, ct));
}
