namespace PersonalVidIQ.Domain.Entities;

/// <summary>One refresh of tracked items (refresh_tracked or `personalvidiq collect`). Not YouTube data.</summary>
public sealed class CollectionRun
{
    public long Id { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public required string Trigger { get; set; }

    public bool Forced { get; set; }

    /// <summary>Running, Succeeded, Partial or Failed.</summary>
    public required string Status { get; set; }

    public int ChannelsRefreshed { get; set; }

    public int VideosRefreshed { get; set; }

    public int NewVideosTracked { get; set; }

    public int SnapshotsSaved { get; set; }

    public int UnitsUsed { get; set; }

    public string? Error { get; set; }
}
