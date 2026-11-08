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

    /// <summary>
    /// A new snapshot is skipped when the previous one for the same video or channel is younger than this.
    /// Makes repeated refreshes idempotent.
    /// </summary>
    [Range(0, 1440)]
    public int MinSnapshotSpacingMinutes { get; set; } = 15;

    /// <summary>Untracked videos and channels whose metadata hasn't been refreshed for this long are deleted (max 30).</summary>
    [Range(1, 30)]
    public int StaleMetadataDays { get; set; } = 30;

    /// <summary>How long quota bookkeeping rows are kept. Not YouTube data.</summary>
    [Range(1, 365)]
    public int QuotaHistoryDays { get; set; } = 7;
}
