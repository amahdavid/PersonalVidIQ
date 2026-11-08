using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.Application.Collection;

public enum VideoTier
{
    /// <summary>Younger than NewVideoWindowHours (48 h): snapshot every few hours.</summary>
    New,

    /// <summary>Up to 30 days old: snapshot daily.</summary>
    Recent,

    /// <summary>Older: snapshot weekly (keeps metadata inside the 30-day refresh rule).</summary>
    Old,
}

/// <summary>Decides what a refresh should collect. DECISIONS.md "Defaults" and #6.</summary>
public sealed class DuePolicy(DataCollectionOptions options)
{
    public VideoTier TierFor(DateTimeOffset? publishedAtUtc, DateTimeOffset nowUtc)
    {
        if (publishedAtUtc is null)
        {
            return VideoTier.Recent;
        }

        var age = nowUtc - publishedAtUtc.Value;
        if (age < TimeSpan.FromHours(options.NewVideoWindowHours))
        {
            return VideoTier.New;
        }

        return age < TimeSpan.FromDays(30) ? VideoTier.Recent : VideoTier.Old;
    }

    public TimeSpan IntervalFor(VideoTier tier) => TimeSpan.FromHours(tier switch
    {
        VideoTier.New => options.NewVideoSnapshotIntervalHours,
        VideoTier.Recent => options.RecentVideoSnapshotIntervalHours,
        _ => options.OldVideoSnapshotIntervalHours,
    });

    public TimeSpan ChannelInterval => TimeSpan.FromHours(options.ChannelSnapshotIntervalHours);

    public TimeSpan UploadsCheckInterval => TimeSpan.FromHours(options.UploadsCheckIntervalHours);

    /// <summary>Due when there's no previous capture, or enough of the interval has passed.</summary>
    public bool IsDue(DateTimeOffset? lastUtc, TimeSpan interval, DateTimeOffset nowUtc) =>
        lastUtc is null || nowUtc - lastUtc.Value >= interval * options.DueTolerance;

    /// <summary>When the item next becomes due (null = due now).</summary>
    public DateTimeOffset? NextDueUtc(DateTimeOffset? lastUtc, TimeSpan interval, DateTimeOffset nowUtc) =>
        IsDue(lastUtc, interval, nowUtc) ? null : lastUtc!.Value + interval * options.DueTolerance;
}
