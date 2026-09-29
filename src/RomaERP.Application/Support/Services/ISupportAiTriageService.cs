namespace RomaERP.Application.Support.Services;

public record SupportTriageResult(
    bool CanAnswer,
    string? AnswerBody,
    string? EscalationReason);

/// <summary>Abstraction over the AI first-response step for a new support ticket (Claude API). Kept
/// separate from the ticket/business logic for the same reason as IClaudeExpenseParser: testable without
/// live API calls, HTTP/API-key concerns stay in Infrastructure.</summary>
public interface ISupportAiTriageService
{
    /// <summary>Attempts to answer a new ticket automatically from general RomaERP product knowledge. Only
    /// ever returns CanAnswer=true for generic "how do I / what is" questions it can answer confidently and
    /// safely without touching the customer's own account or data — anything account-specific, billing, or
    /// bug-shaped is left for a human, never guessed at.</summary>
    Task<SupportTriageResult> TriageAsync(string subject, string body, CancellationToken ct = default);
}
