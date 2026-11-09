using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Application.Services;

/// <summary>Adds and removes channels and videos from tracking. DECISIONS.md #3, #4.</summary>
public sealed class TrackingService(
    IYouTubeDataClient youTube,
    IChannelStore channelStore,
    IVideoStore videoStore,
    ISnapshotStore snapshots,
    ChannelVideoLoader loader,
    IOptions<DataCollectionOptions> options) : ITrackingService
{
    public async Task<TrackChannelResult> TrackChannelAsync(string channel, CancellationToken cancellationToken)
    {
        var data = await loader.GetChannelAsync(channel, cancellationToken);
        var wasTracked = (await channelStore.FindAsync(data.Id, cancellationToken))?.IsTracked ?? false;

        await channelStore.UpsertAsync(data, cancellationToken);
        await channelStore.SetTrackedAsync(data.Id, true, cancellationToken);
        var saved = await snapshots.AddChannelSnapshotsAsync([data], cancellationToken);

        var uploads = options.Value.InitialUploadsToTrack > 0
            ? await loader.GetRecentUploadsAsync(data, options.Value.InitialUploadsToTrack, cancellationToken)
            : [];
        await videoStore.UpsertAsync(uploads, TrackingSource.Channel, cancellationToken);
        saved += await snapshots.AddVideoSnapshotsAsync(uploads, cancellationToken);
        await channelStore.MarkUploadsCheckedAsync(data.Id, cancellationToken);

        return new TrackChannelResult(
            ChannelVideoLoader.Summarize(data),
            wasTracked,
            uploads.Count,
            saved,
            [
                uploads.Count == 0
                    ? ChannelVideoLoader.NoUploadsNote + " New uploads will be picked up whenever tracked items are refreshed."
                    : $"Tracking the latest {(uploads.Count == 1 ? "upload" : $"{uploads.Count} uploads")} now, and new uploads whenever tracked items are refreshed.",
                "Velocity and growth need at least two snapshots: refresh again later (hours apart) to start measuring.",
                $"Snapshots are kept for {options.Value.SnapshotRetentionDays} days.",
            ]);
    }

    public async Task<UntrackResult> UntrackChannelAsync(string channel, CancellationToken cancellationToken)
    {
        var parsed = YouTubeInput.ParseChannel(channel);
        var stored = parsed.Kind switch
        {
            ChannelRefKind.Id => await channelStore.FindAsync(parsed.Value, cancellationToken),
            ChannelRefKind.Handle => await channelStore.FindByHandleAsync(parsed.Value, cancellationToken),
            _ => await channelStore.FindAsync((await youTube.GetChannelAsync(parsed, cancellationToken)).Id, cancellationToken),
        };

        if (stored is null || !stored.IsTracked)
        {
            return new UntrackResult(channel, false, 0, ["That channel isn't tracked."]);
        }

        await channelStore.SetTrackedAsync(stored.YouTubeChannelId, false, cancellationToken);
        var untracked = await videoStore.UntrackChannelVideosAsync(stored.YouTubeChannelId, cancellationToken);
        return new UntrackResult(stored.Title, true, untracked,
        [
            "Videos you tracked individually are still tracked.",
            $"Stored data isn't deleted straight away; it ages out within {options.Value.SnapshotRetentionDays} days.",
        ]);
    }

    public async Task<TrackVideoResult> TrackVideoAsync(string video, CancellationToken cancellationToken)
    {
        var id = YouTubeInput.ParseVideoId(video);
        var batch = await youTube.GetVideosAsync([id], cancellationToken);
        var data = batch.Videos.SingleOrDefault()
            ?? throw new YouTubeApiException(YouTubeErrorKind.NotFound, $"Video {id} wasn't found or isn't public.");

        // Store the channel (untracked) so the video links to it and outlier baselines can find it.
        if (!string.IsNullOrEmpty(data.ChannelId) && await channelStore.FindAsync(data.ChannelId, cancellationToken) is null)
        {
            var channels = await youTube.GetChannelsAsync([data.ChannelId], cancellationToken);
            if (channels.Count == 1)
            {
                await channelStore.UpsertAsync(channels[0], cancellationToken);
            }
        }

        var wasTracked = (await videoStore.FindAsync(id, cancellationToken))?.IsTracked ?? false;
        await videoStore.UpsertAsync([data], TrackingSource.Manual, cancellationToken);
        var saved = await snapshots.AddVideoSnapshotsAsync([data], cancellationToken);

        return new TrackVideoResult(loader.Summarize(data, true), wasTracked, saved,
        [
            "Velocity needs at least two snapshots: refresh again later to start measuring.",
            $"Snapshots are kept for {options.Value.SnapshotRetentionDays} days.",
        ]);
    }

    public async Task<UntrackResult> UntrackVideoAsync(string video, CancellationToken cancellationToken)
    {
        var id = YouTubeInput.ParseVideoId(video);
        var stored = await videoStore.FindAsync(id, cancellationToken);
        if (stored is null || !stored.IsTracked)
        {
            return new UntrackResult(id, false, 0, ["That video isn't tracked."]);
        }

        await videoStore.SetTrackedAsync(id, false, cancellationToken);
        var notes = new List<string> { $"Stored data ages out within {options.Value.SnapshotRetentionDays} days." };
        if (stored.TrackingSource == TrackingSource.Channel)
        {
            notes.Add("Its channel is still tracked; new uploads keep being added, but this video stays untracked.");
        }

        return new UntrackResult(stored.Title, true, 1, notes);
    }

    public async Task<TrackedList> ListTrackedAsync(CancellationToken cancellationToken)
    {
        var channels = await channelStore.ListTrackedAsync(cancellationToken);
        var videos = await videoStore.ListTrackedAsync(cancellationToken);
        var perChannel = videos.Where(v => v.Channel is not null)
            .GroupBy(v => v.Channel!.YouTubeChannelId)
            .ToDictionary(g => g.Key, g => g.Count());

        var channelItems = channels.Select(c => new TrackedChannelItem(
            new ChannelRefSummary(c.YouTubeChannelId, c.Title, c.CustomUrl, c.CustomUrl is { Length: > 0 } h && h.StartsWith('@')
                ? $"https://www.youtube.com/{h}"
                : $"https://www.youtube.com/channel/{c.YouTubeChannelId}"),
            c.TrackedAtUtc,
            perChannel.GetValueOrDefault(c.YouTubeChannelId),
            c.LastUploadsCheckAtUtc,
            c.LastRefreshedAtUtc)).ToList();

        var manual = videos.Where(v => v.TrackingSource == TrackingSource.Manual)
            .Select(v => new TrackedVideoItem(v.YouTubeVideoId, v.Title, v.Channel?.Title, v.PublishedAtUtc, "manual", v.LastRefreshedAtUtc))
            .ToList();

        var notes = new List<string>();
        if (channelItems.Count == 0 && manual.Count == 0)
        {
            notes.Add("Nothing is tracked yet. Use track_channel or track_video.");
        }

        return new TrackedList(channelItems, manual, videos.Count, notes);
    }
}
