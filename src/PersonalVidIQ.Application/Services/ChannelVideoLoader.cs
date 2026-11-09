using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Services;

/// <summary>
/// Fetches fresh public data and pairs it with stored history. Any fresh read of a video or channel that is
/// already stored is also saved as a snapshot, so analysing tracked items builds their history for free.
/// </summary>
public sealed class ChannelVideoLoader(
    IYouTubeDataClient youTube,
    ISnapshotStore snapshots,
    IOptions<DataCollectionOptions> options,
    TimeProvider timeProvider)
{
    public const int MaxUploads = 200;

    public const string NoUploadsNote = "This channel has no public uploads.";

    private DataCollectionOptions Options => options.Value;

    public async Task<ChannelData> GetChannelAsync(string channel, CancellationToken cancellationToken) =>
        await youTube.GetChannelAsync(YouTubeInput.ParseChannel(channel), cancellationToken);

    /// <summary>The channel's most recent uploads (newest first) with fresh stats. 1 unit per 50 videos, twice.</summary>
    public async Task<IReadOnlyList<VideoData>> GetRecentUploadsAsync(ChannelData channel, int maxVideos, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(channel.UploadsPlaylistId))
        {
            return [];
        }

        IReadOnlyList<PlaylistVideo> playlist;
        try
        {
            playlist = await youTube.GetPlaylistVideosAsync(channel.UploadsPlaylistId, Math.Clamp(maxVideos, 1, MaxUploads), cancellationToken);
        }
        catch (YouTubeApiException ex) when (ex.Kind == YouTubeErrorKind.NotFound)
        {
            // YouTube reports playlistNotFound for the uploads playlist of a channel that has never uploaded.
            return [];
        }

        if (playlist.Count == 0)
        {
            return [];
        }

        var batch = await youTube.GetVideosAsync(playlist.Select(p => p.VideoId).ToList(), cancellationToken);
        return batch.Videos.OrderByDescending(v => v.PublishedAtUtc).ToList();
    }

    /// <summary>Loads stored history (within the retention window), then saves the fresh reads as snapshots for stored items.</summary>
    public async Task<IReadOnlyList<VideoAnalyticsInput>> BuildInputsAsync(IReadOnlyList<VideoData> videos, CancellationToken cancellationToken)
    {
        var since = timeProvider.GetUtcNow().AddDays(-Options.SnapshotRetentionDays);
        var histories = await snapshots.GetVideoHistoriesAsync(videos.Select(v => v.Id).ToList(), since, cancellationToken);
        await snapshots.AddVideoSnapshotsAsync(videos, cancellationToken);

        return videos.Select(v => ToInput(v, histories.TryGetValue(v.Id, out var h) ? h : [])).ToList();
    }

    public async Task RecordChannelAsync(ChannelData channel, CancellationToken cancellationToken) =>
        await snapshots.AddChannelSnapshotsAsync([channel], cancellationToken);

    public static VideoAnalyticsInput ToInput(VideoData v, IReadOnlyList<Observation> history) => new(
        v.Id, v.Title, v.PublishedAtUtc, v.Duration, v.LiveBroadcastContent, v.ViewCount, v.LikeCount, v.CommentCount, v.RetrievedAtUtc, history);

    public VideoSummary Summarize(VideoData v, bool isTracked) => new(
        v.Id,
        $"https://www.youtube.com/watch?v={v.Id}",
        v.Title,
        v.ChannelId,
        v.ChannelTitle,
        v.PublishedAtUtc,
        v.Duration is { } d ? (int)Math.Round(d.TotalSeconds) : null,
        VideoFormatClassifier.Classify(v.Duration, Options.ShortsMaxDurationSeconds),
        isTracked);

    public static ChannelRefSummary Summarize(ChannelData c) =>
        new(c.Id, c.Title, c.CustomUrl, c.CustomUrl is { Length: > 0 } handle && handle.StartsWith('@')
            ? $"https://www.youtube.com/{handle}"
            : $"https://www.youtube.com/channel/{c.Id}");

    public OutlierSettings OutlierSettings => new(ShortsMaxDurationSeconds: Options.ShortsMaxDurationSeconds);
}
