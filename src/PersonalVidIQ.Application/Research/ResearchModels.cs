using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Research;

/// <summary>A stored video with what keyword and trend research needs.</summary>
public sealed record ResearchVideo(
    VideoAnalyticsInput Input,
    string ChannelId,
    string ChannelTitle,
    IReadOnlyList<string> Tags,
    VideoFormat Format,
    double? ChannelRelativeRatio,
    double? LifetimeViewsPerDay);

public sealed record VideoEvidence(
    string VideoId,
    string Title,
    string ChannelTitle,
    DateTimeOffset? PublishedAtUtc,
    VideoFormat Format,
    long? Views,
    double? LifetimeViewsPerDay,
    double? ChannelRelativeRatio,
    string Url);

public sealed record TermStat(
    string Term,
    int Videos,
    int Channels,
    int ScoredVideos,
    double? MedianChannelRelativeRatio,
    double? MedianLifetimeViewsPerDay,
    Confidence Confidence,
    IReadOnlyList<VideoEvidence> Examples);

public sealed record TopicGroup(
    string Label,
    IReadOnlyList<string> Terms,
    int Videos,
    int Channels,
    double? MedianChannelRelativeRatio,
    Confidence Confidence,
    IReadOnlyList<VideoEvidence> Examples);

public sealed record EmergingTerm(
    string Term,
    int RecentVideos,
    int PriorVideos,
    int RecentChannels,
    double? RecentMedianChannelRelativeRatio,
    string Signal,
    Confidence Confidence,
    IReadOnlyList<VideoEvidence> Examples);

public static class Evidence
{
    public static VideoEvidence From(ResearchVideo v) => new(
        v.Input.VideoId,
        v.Input.Title,
        v.ChannelTitle,
        v.Input.PublishedAtUtc,
        v.Format,
        v.Input.Views,
        v.LifetimeViewsPerDay is { } d ? Statistics.Round(d) : null,
        v.ChannelRelativeRatio is { } r ? Statistics.Round(r) : null,
        $"https://www.youtube.com/watch?v={v.Input.VideoId}");
}
