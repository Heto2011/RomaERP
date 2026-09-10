namespace RomaERP.Application.Assistant.Services;

public record ExpenseAccountCandidate(string Code, string NameAr);

public record ExpenseExtractionResult(
    decimal? Amount,
    string? Currency,
    string? Description,
    string? SuggestedAccountCode,
    bool NeedsClarification,
    string? ClarifyingQuestion,
    DateTime? EntryDate = null);

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

    /// <summary>OCR + extraction in one step: sends a photo of a receipt/invoice to Claude's vision-capable
    /// Messages API and asks for the same structured fields ExtractAsync produces, plus the receipt's own
    /// date when legible. <paramref name="mediaType"/> must be one of the types Claude's API accepts for
    /// images (image/jpeg, image/png, image/webp, image/gif).</summary>
    Task<ExpenseExtractionResult> ExtractFromReceiptImageAsync(
        byte[] imageBytes,
        string mediaType,
        IReadOnlyList<ExpenseAccountCandidate> expenseAccounts,
        CancellationToken ct = default);
}
