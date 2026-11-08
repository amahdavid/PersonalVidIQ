namespace PersonalVidIQ.Application.Interfaces;

/// <summary>Enforces the 30-day storage rule (YouTube API Developer Policies III.E.4). DECISIONS.md #2.</summary>
public interface IRetentionService
{
    Task<RetentionResult> PurgeAsync(CancellationToken cancellationToken);
}

public sealed record RetentionResult(
    int VideoSnapshotsDeleted,
    int ChannelSnapshotsDeleted,
    int StaleVideosDeleted,
    int StaleChannelsDeleted,
    int SearchCacheEntriesDeleted,
    int QuotaRowsDeleted,
    DateTimeOffset RanAtUtc)
{
    public int Total => VideoSnapshotsDeleted + ChannelSnapshotsDeleted + StaleVideosDeleted + StaleChannelsDeleted + SearchCacheEntriesDeleted + QuotaRowsDeleted;
}
