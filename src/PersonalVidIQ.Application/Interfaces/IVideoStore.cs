using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Domain.Entities;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Application.Interfaces;

public interface IVideoStore
{
    /// <summary>
    /// Inserts or updates video metadata and stamps LastRefreshedAtUtc. Videos are linked to a stored channel
    /// when one exists. When <paramref name="trackAs"/> is set, untracked videos become tracked with that source;
    /// already tracked videos keep their original source.
    /// </summary>
    Task<IReadOnlyList<Video>> UpsertAsync(IReadOnlyCollection<VideoData> videos, TrackingSource? trackAs, CancellationToken cancellationToken);

    Task<Video?> FindAsync(string youTubeVideoId, CancellationToken cancellationToken);

    /// <summary>Returns false when the video isn't stored.</summary>
    Task<bool> SetTrackedAsync(string youTubeVideoId, bool tracked, CancellationToken cancellationToken);

    /// <summary>Untracks every video tracked through this channel (manual tracks are kept). Returns the count.</summary>
    Task<int> UntrackChannelVideosAsync(string youTubeChannelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Video>> ListTrackedAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Video>> ListByChannelAsync(string youTubeChannelId, CancellationToken cancellationToken);
}
