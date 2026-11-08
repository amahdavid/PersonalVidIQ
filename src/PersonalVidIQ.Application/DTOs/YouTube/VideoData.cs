namespace PersonalVidIQ.Application.DTOs.YouTube;

/// <summary>A video as returned by videos.list. Counts are null when YouTube hides them.</summary>
public sealed record VideoData(
    string Id,
    string ChannelId,
    string ChannelTitle,
    string Title,
    string Description,
    DateTimeOffset? PublishedAtUtc,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    IReadOnlyList<string> Tags,
    string? CategoryId,
    string? LiveBroadcastContent,
    long? ViewCount,
    long? LikeCount,
    long? CommentCount,
    DateTimeOffset RetrievedAtUtc);

/// <summary>Result of a batched videos.list lookup. Ids YouTube did not return are listed in <see cref="MissingIds"/>.</summary>
public sealed record VideoBatch(IReadOnlyList<VideoData> Videos, IReadOnlyList<string> MissingIds);
