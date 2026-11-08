using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.Application.Interfaces;

public interface ISnapshotStore
{
    /// <summary>
    /// Stores one snapshot per video from the counts in <paramref name="videos"/> (captured at their RetrievedAtUtc).
    /// Idempotent: a video that already has a snapshot within the minimum spacing is skipped. Videos not stored are skipped.
    /// Returns how many snapshots were written.
    /// </summary>
    Task<int> AddVideoSnapshotsAsync(IReadOnlyCollection<VideoData> videos, CancellationToken cancellationToken);

    /// <summary>Same rules as <see cref="AddVideoSnapshotsAsync"/>, for channels.</summary>
    Task<int> AddChannelSnapshotsAsync(IReadOnlyCollection<ChannelData> channels, CancellationToken cancellationToken);

    /// <summary>Snapshots for a video, oldest first, optionally limited to those captured at or after <paramref name="sinceUtc"/>.</summary>
    Task<IReadOnlyList<VideoSnapshot>> GetVideoSnapshotsAsync(string youTubeVideoId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken);

    /// <summary>View-count history for many videos at once, keyed by YouTube video id. Videos with no snapshots are absent.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<Observation>>> GetVideoHistoriesAsync(
        IReadOnlyCollection<string> youTubeVideoIds, DateTimeOffset? sinceUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChannelSnapshot>> GetChannelSnapshotsAsync(string youTubeChannelId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken);
}
