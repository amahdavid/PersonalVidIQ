using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Application.DTOs.Analysis;

/// <summary>Counts exactly as the YouTube Data API returned them. Null means hidden or unavailable.</summary>
public sealed record YouTubeVideoStats(long? Views, long? Likes, long? Comments, DateTimeOffset CapturedAtUtc)
{
    public MetricSource Source => MetricSource.YouTubeApi;
}

public sealed record VideoSummary(
    string VideoId,
    string Url,
    string Title,
    string ChannelId,
    string ChannelTitle,
    DateTimeOffset? PublishedAtUtc,
    int? DurationSeconds,
    VideoFormat Format,
    bool IsTracked);

public sealed record HistorySummary(int SnapshotCount, DateTimeOffset? FirstUtc, DateTimeOffset? LastUtc, int RetentionDays);

public sealed record VideoAnalysis(
    VideoSummary Video,
    YouTubeVideoStats YouTube,
    IReadOnlyList<CalculatedMetric> Calculated,
    OutlierResult? Outlier,
    HistorySummary History,
    IReadOnlyList<string> Limitations);

public sealed record VideoComparison(IReadOnlyList<VideoAnalysis> Videos, IReadOnlyList<string> NotFound, IReadOnlyList<string> Limitations);

public sealed record OutlierQuery(
    string Channel,
    int PublishedWithinDays = 30,
    VideoFormat? Format = null,
    int MaxVideos = 50,
    double MinRatio = 1.5);

public sealed record ChannelRefSummary(string ChannelId, string Title, string? Handle, string Url);

public sealed record OutlierReport(
    ChannelRefSummary Channel,
    OutlierQuery Query,
    int VideosChecked,
    int VideosInWindow,
    IReadOnlyList<OutlierResult> Outliers,
    IReadOnlyList<OutlierResult> NotScored,
    string Formula,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<string> Notes);

public sealed record ChannelStats(
    long? Subscribers,
    bool SubscribersHidden,
    long? TotalViews,
    long? VideoCount,
    DateTimeOffset CapturedAtUtc)
{
    public MetricSource Source => MetricSource.YouTubeApi;
}

public sealed record ChannelPeriodActivity(
    int PeriodDays,
    int UploadsChecked,
    int UploadsInPeriod,
    int ShortsInPeriod,
    int LongFormInPeriod,
    CalculatedMetric MedianShortViewsPerDay,
    CalculatedMetric MedianLongFormViewsPerDay,
    CalculatedMetric UploadsPerWeek);

public sealed record ChannelGrowth(
    int SnapshotsInPeriod,
    CalculatedMetric TotalViewsPerDay,
    CalculatedMetric SubscriberChange);

public sealed record ChannelComparisonEntry(
    ChannelRefSummary Channel,
    ChannelStats YouTube,
    ChannelPeriodActivity Activity,
    ChannelGrowth Growth,
    bool IsTracked);

public sealed record ChannelComparison(
    IReadOnlyList<ChannelComparisonEntry> Channels,
    IReadOnlyList<string> NotFound,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<string> Limitations);
