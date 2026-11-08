using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Interfaces;

/// <summary>
/// Typed access to the YouTube Data API v3 (public data, API key only).
/// Every call is recorded against the daily quota. Failures throw <see cref="YouTubeApiException"/>.
/// </summary>
public interface IYouTubeDataClient
{
    /// <summary>search.list, type=video (100 units). Subject to the daily search cap; repeat queries are served from cache.</summary>
    Task<SearchResult> SearchVideosAsync(SearchRequest request, CancellationToken cancellationToken);

    /// <summary>channels.list by id, batched 50 per call (1 unit per call). Unknown ids are omitted.</summary>
    Task<IReadOnlyList<ChannelData>> GetChannelsAsync(IReadOnlyCollection<string> channelIds, CancellationToken cancellationToken);

    /// <summary>Resolves an id, @handle or legacy username to a channel (1 unit). Throws NotFound when it doesn't exist.</summary>
    Task<ChannelData> GetChannelAsync(ChannelRef channel, CancellationToken cancellationToken);

    /// <summary>videos.list, batched 50 per call (1 unit per call).</summary>
    Task<VideoBatch> GetVideosAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken);

    /// <summary>playlistItems.list over a channel's uploads playlist, newest first, paginated 50 per call (1 unit per call).</summary>
    Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(string playlistId, int maxResults, CancellationToken cancellationToken);
}
