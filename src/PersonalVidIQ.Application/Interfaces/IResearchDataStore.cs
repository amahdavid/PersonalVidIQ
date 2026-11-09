using PersonalVidIQ.Application.Analytics;

namespace PersonalVidIQ.Application.Interfaces;

/// <summary>A stored video with its snapshot history (within the retention window), for research over stored data.</summary>
public sealed record StoredVideoRecord(
    string VideoId,
    string ChannelId,
    string ChannelTitle,
    string Title,
    IReadOnlyList<string> Tags,
    DateTimeOffset? PublishedAtUtc,
    int? DurationSeconds,
    string? LiveBroadcastContent,
    IReadOnlyList<Observation> Snapshots);

public interface IResearchDataStore
{
    /// <summary>
    /// Every stored video of the given channels (or of all tracked channels when null), tracked or not,
    /// with view-count snapshots captured at or after <paramref name="snapshotsSinceUtc"/>. No quota.
    /// </summary>
    Task<IReadOnlyList<StoredVideoRecord>> GetChannelVideosAsync(
        IReadOnlyCollection<string>? channelIds, DateTimeOffset snapshotsSinceUtc, CancellationToken cancellationToken);
}
