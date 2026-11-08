using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Interfaces;

public interface IResearchService
{
    /// <summary>search.list (100 units, capped and cached) enriched with videos.list stats (1 unit).</summary>
    Task<SearchReport> SearchAsync(SearchRequest request, CancellationToken cancellationToken);

    Task<ChannelReport> GetChannelAsync(string channel, CancellationToken cancellationToken);

    Task<ChannelVideosReport> GetChannelVideosAsync(string channel, int maxResults, ChannelVideoOrder order, VideoFormat? format, CancellationToken cancellationToken);

    Task<VideoReport> GetVideoAsync(string video, CancellationToken cancellationToken);
}

public interface ITrackingService
{
    Task<TrackChannelResult> TrackChannelAsync(string channel, CancellationToken cancellationToken);

    Task<UntrackResult> UntrackChannelAsync(string channel, CancellationToken cancellationToken);

    Task<TrackVideoResult> TrackVideoAsync(string video, CancellationToken cancellationToken);

    Task<UntrackResult> UntrackVideoAsync(string video, CancellationToken cancellationToken);

    Task<TrackedList> ListTrackedAsync(CancellationToken cancellationToken);
}
