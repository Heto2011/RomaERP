namespace RomaERP.Application.Assistant.Services;

public record ExpenseAccountCandidate(string Code, string NameAr);

public record ExpenseExtractionResult(
    decimal? Amount,
    string? Currency,
    string? Description,
    string? SuggestedAccountCode,
    bool NeedsClarification,
    string? ClarifyingQuestion);

/// <summary>
/// Abstraction over the natural-language understanding step (Claude API). Kept separate from
/// the conversation/business logic so the assistant flow can be tested without live API calls,
/// and so the HTTP/API-key concerns stay in Infrastructure.
/// </summary>
public interface IClaudeExpenseParser
{
    Task<ExpenseExtractionResult> ExtractAsync(
        string userMessage,
        string? priorContext,
        IReadOnlyList<ExpenseAccountCandidate> expenseAccounts,
        CancellationToken ct = default);
}
