namespace PersonalVidIQ.Application.DTOs.YouTube;

public enum SearchOrder
{
    Relevance,
    Date,
    ViewCount,
    Rating,
}

/// <summary>search.list videoDuration filter. YouTube's "short" means under 4 minutes, not "is a Short".</summary>
public enum SearchDuration
{
    Any,
    Short,
    Medium,
    Long,
}

public sealed record SearchRequest(
    string Query,
    int? MaxResults = null,
    SearchOrder Order = SearchOrder.Relevance,
    DateTimeOffset? PublishedAfterUtc = null,
    string? RegionCode = null,
    SearchDuration Duration = SearchDuration.Any);

public sealed record SearchHit(
    string VideoId,
    string ChannelId,
    string ChannelTitle,
    string Title,
    string Description,
    DateTimeOffset? PublishedAtUtc,
    string? ThumbnailUrl);

public sealed record SearchResult(
    SearchRequest Request,
    IReadOnlyList<SearchHit> Hits,
    long? TotalResultsEstimate,
    bool FromCache,
    DateTimeOffset RetrievedAtUtc);
