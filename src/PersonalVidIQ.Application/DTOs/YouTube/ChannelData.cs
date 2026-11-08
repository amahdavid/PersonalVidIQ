namespace PersonalVidIQ.Application.DTOs.YouTube;

/// <summary>A channel as returned by channels.list. Counts are null when YouTube hides them.</summary>
public sealed record ChannelData(
    string Id,
    string Title,
    string Description,
    string? CustomUrl,
    string? ThumbnailUrl,
    DateTimeOffset? PublishedAtUtc,
    string? Country,
    string? UploadsPlaylistId,
    long? SubscriberCount,
    bool SubscriberCountHidden,
    long? ViewCount,
    long? VideoCount,
    DateTimeOffset RetrievedAtUtc);
