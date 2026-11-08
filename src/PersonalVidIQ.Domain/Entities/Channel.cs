namespace PersonalVidIQ.Domain.Entities;

/// <summary>
/// A YouTube channel PersonalVidIQ knows about. Only tracked channels are snapshotted; untracked
/// rows exist because a stored video belongs to them, and are purged after 30 days without refresh.
/// </summary>
public sealed class Channel
{
    public long Id { get; set; }

    public required string YouTubeChannelId { get; set; }

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? CustomUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public string? Country { get; set; }

    public string? UploadsPlaylistId { get; set; }

    public bool IsTracked { get; set; }

    public DateTimeOffset? TrackedAtUtc { get; set; }

    /// <summary>When the uploads playlist was last checked for new videos.</summary>
    public DateTimeOffset? LastUploadsCheckAtUtc { get; set; }

    /// <summary>When metadata was last fetched from YouTube. Drives the 30-day refresh-or-delete rule.</summary>
    public DateTimeOffset LastRefreshedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<ChannelSnapshot> Snapshots { get; set; } = [];

    public List<Video> Videos { get; set; } = [];
}
