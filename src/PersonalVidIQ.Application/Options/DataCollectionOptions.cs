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

    /// <summary>How often a tracked channel's uploads playlist is checked for new videos.</summary>
    [Range(1, 168)]
    public int UploadsCheckIntervalHours { get; set; } = 3;

    /// <summary>How many of the newest uploads are looked at when checking for new videos.</summary>
    [Range(1, 50)]
    public int UploadsCheckDepth { get; set; } = 20;

    /// <summary>
    /// An item counts as due when this fraction of its interval has passed, so refreshing a little early
    /// (e.g. 2 h 45 m into a 3 h interval) still collects it.
    /// </summary>
    [Range(0.5, 1.0)]
    public double DueTolerance { get; set; } = 0.9;

    /// <summary>Quota units a refresh leaves untouched for searches and lookups.</summary>
    [Range(0, 10_000)]
    public int QuotaReserveUnits { get; set; } = 1_000;

    /// <summary>How long quota bookkeeping rows are kept. Not YouTube data.</summary>
    [Range(1, 365)]
    public int QuotaHistoryDays { get; set; } = 7;
}
