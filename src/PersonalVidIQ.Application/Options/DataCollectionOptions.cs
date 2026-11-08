using System.ComponentModel.DataAnnotations;

namespace PersonalVidIQ.Application.Options;

public sealed class DataCollectionOptions
{
    public const string SectionName = "DataCollection";

    /// <summary>
    /// Snapshots older than this are deleted. Capped at 30 by YouTube API Developer Policies
    /// III.E.4 for non-authorized statistics. DECISIONS.md #2.
    /// </summary>
    [Range(1, 30)]
    public int SnapshotRetentionDays { get; set; } = 30;

    /// <summary>Videos at or under this duration are treated as Shorts. DECISIONS.md #9.</summary>
    [Range(1, 3600)]
    public int ShortsMaxDurationSeconds { get; set; } = 180;

    /// <summary>How many recent uploads are tracked when a channel is first tracked. DECISIONS.md #4.</summary>
    [Range(0, 500)]
    public int InitialUploadsToTrack { get; set; } = 50;

    /// <summary>Videos younger than this are "new" and refreshed more often.</summary>
    [Range(1, 720)]
    public int NewVideoWindowHours { get; set; } = 48;

    [Range(1, 168)]
    public int NewVideoSnapshotIntervalHours { get; set; } = 3;

    [Range(1, 720)]
    public int RecentVideoSnapshotIntervalHours { get; set; } = 24;

    [Range(1, 2160)]
    public int OldVideoSnapshotIntervalHours { get; set; } = 168;

    [Range(1, 720)]
    public int ChannelSnapshotIntervalHours { get; set; } = 24;
}
