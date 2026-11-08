using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.Application.Interfaces;

/// <summary>Read-side queries the collector needs.</summary>
public interface ICollectionStateStore
{
    Task<IReadOnlyList<TrackedChannelState>> GetTrackedChannelsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackedVideoState>> GetTrackedVideosAsync(CancellationToken cancellationToken);

    /// <summary>Which of these video ids are already stored (tracked or not).</summary>
    Task<IReadOnlySet<string>> GetStoredVideoIdsAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken);
}

public interface ICollectionRunLog
{
    Task<long> StartAsync(string trigger, bool forced, CancellationToken cancellationToken);

    Task CompleteAsync(long runId, CollectionRun result, CancellationToken cancellationToken);

    Task<CollectionRun?> GetLastAsync(CancellationToken cancellationToken);
}

public interface ICollectionService
{
    /// <summary>Refreshes tracked items that are due (or all with Force), within the day's quota budget.</summary>
    Task<CollectionReport> CollectAsync(CollectRequest request, CancellationToken cancellationToken);
}
