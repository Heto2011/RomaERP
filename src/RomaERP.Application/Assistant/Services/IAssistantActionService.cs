using RomaERP.Application.Assistant.DTOs;

namespace RomaERP.Application.Assistant.Services;

public interface IClaudeActionParser
{
    Task<ActionIntent> ParseAsync(string text, CancellationToken ct = default);
}

/// <summary>Lets the owner ask the assistant to prepare an invoice, a customer payment or a menu price change from one sentence.
/// The assistant only prepares; the owner reviews, edits and approves, and the existing services do the booking.</summary>
public interface IAssistantActionService
{
    Task<ActionProposalDto> ProposeAsync(string text, CancellationToken ct = default);
    Task<ActionProposalDto> ProposeFromIntentAsync(ActionIntent intent, CancellationToken ct = default);
    /// <summary>Reads a photo of a supplier invoice (mode "vendor_invoice") or a receipt (mode "receipt") into a draft for the owner to review.</summary>
    Task<ActionProposalDto> ProposeFromImageAsync(byte[] bytes, string mediaType, string mode, CancellationToken ct = default);
    Task<ExecuteActionResultDto> ExecuteAsync(ExecuteActionRequestDto request, CancellationToken ct = default);
}
