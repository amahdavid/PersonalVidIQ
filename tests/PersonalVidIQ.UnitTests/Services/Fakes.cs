using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Domain.Entities;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.UnitTests.Services;

internal sealed class FakeYouTube : IYouTubeDataClient
{
    public Dictionary<string, ChannelData> Channels { get; } = new();
    public Dictionary<string, VideoData> Videos { get; } = new();
    public Dictionary<string, List<string>> Playlists { get; } = new();
    public int Calls { get; private set; }

    public Task<SearchResult> SearchVideosAsync(SearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ChannelData>> GetChannelsAsync(IReadOnlyCollection<string> channelIds, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<ChannelData>>(channelIds.Where(Channels.ContainsKey).Select(id => Channels[id]).ToList());
    }

    public Task<ChannelData> GetChannelAsync(ChannelRef channel, CancellationToken cancellationToken)
    {
        Calls++;
        var match = Channels.Values.FirstOrDefault(c => c.Id == channel.Value || c.CustomUrl == channel.Value)
            ?? throw new YouTubeApiException(YouTubeErrorKind.NotFound, $"No channel {channel.Value}");
        return Task.FromResult(match);
    }

    public Task<VideoBatch> GetVideosAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken)
    {
        Calls++;
        var found = videoIds.Where(Videos.ContainsKey).Select(id => Videos[id]).ToList();
        return Task.FromResult(new VideoBatch(found, videoIds.Where(id => !Videos.ContainsKey(id)).ToList()));
    }

    public Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(string playlistId, int maxResults, CancellationToken cancellationToken)
    {
        Calls++;
        var ids = Playlists.TryGetValue(playlistId, out var list) ? list : [];
        return Task.FromResult<IReadOnlyList<PlaylistVideo>>(ids.Take(maxResults).Select(id => new PlaylistVideo(id, "", Videos[id].PublishedAtUtc)).ToList());
    }
}

internal sealed class FakeSnapshots : ISnapshotStore
{
    public Dictionary<string, List<Observation>> VideoHistory { get; } = new();
    public List<ChannelSnapshot> ChannelHistory { get; } = [];
    public List<VideoData> VideoWrites { get; } = [];
    public List<ChannelData> ChannelWrites { get; } = [];

    public Task<int> AddVideoSnapshotsAsync(IReadOnlyCollection<VideoData> videos, CancellationToken cancellationToken)
    {
        VideoWrites.AddRange(videos);
        return Task.FromResult(videos.Count);
    }

    public Task<int> AddChannelSnapshotsAsync(IReadOnlyCollection<ChannelData> channels, CancellationToken cancellationToken)
    {
        ChannelWrites.AddRange(channels);
        return Task.FromResult(channels.Count);
    }

    public Task<IReadOnlyList<VideoSnapshot>> GetVideoSnapshotsAsync(string youTubeVideoId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, IReadOnlyList<Observation>>> GetVideoHistoriesAsync(
        IReadOnlyCollection<string> youTubeVideoIds, DateTimeOffset? sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<Observation>>>(
            youTubeVideoIds.Where(VideoHistory.ContainsKey)
                .ToDictionary(id => id, id => (IReadOnlyList<Observation>)VideoHistory[id].Where(o => sinceUtc is null || o.AtUtc >= sinceUtc).ToList()));

    public Task<IReadOnlyList<ChannelSnapshot>> GetChannelSnapshotsAsync(string youTubeChannelId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ChannelSnapshot>>(ChannelHistory.Where(s => sinceUtc is null || s.CapturedAtUtc >= sinceUtc).ToList());
}

internal sealed class FakeVideoStore : IVideoStore
{
    public HashSet<string> Tracked { get; } = [];

    public Task<Video?> FindAsync(string youTubeVideoId, CancellationToken cancellationToken) =>
        Task.FromResult(Tracked.Contains(youTubeVideoId) ? new Video { YouTubeVideoId = youTubeVideoId, Title = "", IsTracked = true } : null);

    public Task<IReadOnlyList<Video>> UpsertAsync(IReadOnlyCollection<VideoData> videos, TrackingSource? trackAs, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> SetTrackedAsync(string youTubeVideoId, bool tracked, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int> UntrackChannelVideosAsync(string youTubeChannelId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<Video>> ListTrackedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<Video>> ListByChannelAsync(string youTubeChannelId, CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class FakeChannelStore : IChannelStore
{
    public HashSet<string> Tracked { get; } = [];

    public Task<Channel?> FindAsync(string youTubeChannelId, CancellationToken cancellationToken) =>
        Task.FromResult(Tracked.Contains(youTubeChannelId) ? new Channel { YouTubeChannelId = youTubeChannelId, Title = "", IsTracked = true } : null);

    public Task<Channel?> FindByHandleAsync(string handle, CancellationToken cancellationToken) => Task.FromResult<Channel?>(null);
    public Task<Channel> UpsertAsync(ChannelData data, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> SetTrackedAsync(string youTubeChannelId, bool tracked, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<Channel>> ListTrackedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task MarkUploadsCheckedAsync(string youTubeChannelId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
