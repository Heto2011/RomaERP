using RomaERP.Domain.Common;

namespace RomaERP.Domain.Tenancy;

/// <summary>One hit on a public marketing page (e.g. pricing.html), logged anonymously by the page itself
/// on load — no cookies, no visitor identity, just enough to see traffic volume, which page, and where it
/// came from (Referrer). Lives in the central database since it isn't tied to any tenant.</summary>
public class MarketingPageView : BaseEntity
{
    public DateTime ViewedAtUtc { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
}
