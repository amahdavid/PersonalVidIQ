using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Domain.Entities;

/// <summary>A YouTube video. Statistics live in <see cref="VideoSnapshot"/> rows, never here.</summary>
public sealed class Video
{
    public long Id { get; set; }

    public required string YouTubeVideoId { get; set; }

    public long? ChannelId { get; set; }

    public Channel? Channel { get; set; }

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public int? DurationSeconds { get; set; }

    public string? ThumbnailUrl { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? CategoryId { get; set; }

    public string? LiveBroadcastContent { get; set; }

    public bool IsTracked { get; set; }

    public TrackingSource? TrackingSource { get; set; }

    public DateTimeOffset? TrackedAtUtc { get; set; }

    /// <summary>When metadata was last fetched from YouTube. Drives the 30-day refresh-or-delete rule.</summary>
    public DateTimeOffset LastRefreshedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<VideoSnapshot> Snapshots { get; set; } = [];
}
