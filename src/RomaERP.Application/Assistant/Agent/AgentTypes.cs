using System.Text.Json.Nodes;
using RomaERP.Application.Assistant.DTOs;

namespace RomaERP.Application.Assistant.Agent;

/// <summary>One thing the assistant can do. A read tool returns facts from the books; a write tool only returns a draft for the owner to approve.</summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    /// <summary>JSON schema of the arguments.</summary>
    JsonObject InputSchema { get; }
    Task<AgentToolResult> RunAsync(JsonObject args, CancellationToken ct);
}

public record AgentToolResult(string Text, ActionProposalDto? Draft = null);

public record AgentMessageDto(string Role, string Content);
public record AgentChatRequestDto(List<AgentMessageDto> Messages);
public record AgentChatResponseDto(string Reply, List<ActionProposalDto> Drafts, List<string> ToolsUsed);

/// <summary>Talks to the language model. It returns the model's raw answer: text and/or requests to use tools.</summary>
public interface IAgentModelClient
{
    Task<JsonObject> CompleteAsync(string system, JsonArray messages, JsonArray tools, CancellationToken ct);
}

public interface IAssistantAgentService
{
    Task<AgentChatResponseDto> ChatAsync(AgentChatRequestDto request, CancellationToken ct = default);
}
