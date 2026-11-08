using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.DTOs.Analysis;

public sealed record QuotaSnapshot(int UnitsUsedToday, int UnitsRemainingToday, int SearchesRemainingToday, DateTimeOffset ResetsAtUtc);

/// <summary>A video with its public stats and the one calculated number that needs no history.</summary>
public sealed record VideoListItem(VideoSummary Video, YouTubeVideoStats YouTube, CalculatedMetric LifetimeViewsPerDay);

public sealed record SearchReport(
    string Query,
    string Order,
    bool FromCache,
    long? TotalResultsEstimate,
    IReadOnlyList<VideoListItem> Results,
    QuotaSnapshot Quota,
    IReadOnlyList<string> Notes);

public sealed record ChannelReport(
    ChannelRefSummary Channel,
    string Description,
    DateTimeOffset? CreatedAtUtc,
    string? Country,
    ChannelStats YouTube,
    bool IsTracked,
    int StoredSnapshots,
    IReadOnlyList<string> Notes);

public enum ChannelVideoOrder
{
    Recent,
    Popular,
}

public sealed record ChannelVideosReport(
    ChannelRefSummary Channel,
    ChannelVideoOrder Order,
    VideoFormat? Format,
    int UploadsScanned,
    IReadOnlyList<VideoListItem> Videos,
    IReadOnlyList<string> Notes);

public sealed record VideoReport(
    VideoSummary Video,
    string Description,
    IReadOnlyList<string> Tags,
    YouTubeVideoStats YouTube,
    IReadOnlyList<CalculatedMetric> Calculated,
    IReadOnlyList<string> Notes);

public sealed record TrackChannelResult(
    ChannelRefSummary Channel,
    bool WasAlreadyTracked,
    int RecentUploadsTracked,
    int SnapshotsSaved,
    IReadOnlyList<string> Notes);

public sealed record UntrackResult(string Target, bool Found, int VideosUntracked, IReadOnlyList<string> Notes);

public sealed record TrackVideoResult(VideoSummary Video, bool WasAlreadyTracked, int SnapshotsSaved, IReadOnlyList<string> Notes);

public sealed record TrackedChannelItem(
    ChannelRefSummary Channel,
    DateTimeOffset? TrackedAtUtc,
    int VideosTracked,
    DateTimeOffset? LastUploadsCheckAtUtc,
    DateTimeOffset LastRefreshedAtUtc);

public sealed record TrackedVideoItem(
    string VideoId,
    string Title,
    string? ChannelTitle,
    DateTimeOffset? PublishedAtUtc,
    string TrackedVia,
    DateTimeOffset LastRefreshedAtUtc);

public sealed record TrackedList(
    IReadOnlyList<TrackedChannelItem> Channels,
    IReadOnlyList<TrackedVideoItem> ManuallyTrackedVideos,
    int TotalTrackedVideos,
    IReadOnlyList<string> Notes);
