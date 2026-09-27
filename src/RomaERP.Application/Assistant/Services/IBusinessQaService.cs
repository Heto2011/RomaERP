namespace RomaERP.Application.Assistant.Services;

/// <summary>Answers a free-form question about the tenant's own financial data (e.g. "هل أنا رابح الشهر ده؟")
/// by pulling real numbers from the existing report services and having Claude summarize them in Arabic —
/// never free-text SQL, so the model only ever sees numbers this tenant is already allowed to see.</summary>
public interface IBusinessQaService
{
    Task<string> AskAsync(string question, CancellationToken ct = default);
}
