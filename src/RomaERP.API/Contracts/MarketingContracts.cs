namespace RomaERP.API.Contracts;

public record RecordPageViewRequest(string Path, string? Referrer);

public record MarketingPageViewDto(Guid Id, DateTime ViewedAtUtc, string Path, string? Referrer, string? UserAgent);

public record MarketingPageViewStatsDto(
    int TotalViews,
    int Last7Days,
    int Last30Days,
    List<CountByLabelDto> TopPaths,
    List<CountByLabelDto> TopReferrers);

public record CountByLabelDto(string Label, int Count);
