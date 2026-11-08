using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Services;

public sealed class ResearchService(
    IYouTubeDataClient youTube,
    IQuotaLedger quotaLedger,
    IChannelStore channelStore,
    IVideoStore videoStore,
    ISnapshotStore snapshots,
    ChannelVideoLoader loader,
    IOptions<DataCollectionOptions> options) : IResearchService
{
    private const int MaxDescriptionLength = 1_000;
    private const int PopularScan = 200;

    public async Task<SearchReport> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var result = await youTube.SearchVideosAsync(request, cancellationToken);
        var stats = await youTube.GetVideosAsync(result.Hits.Select(h => h.VideoId).ToList(), cancellationToken);
        var byId = stats.Videos.ToDictionary(v => v.Id, StringComparer.Ordinal);
        var items = result.Hits.Where(h => byId.ContainsKey(h.VideoId)).Select(h => Item(byId[h.VideoId], false)).ToList();
        var usage = await quotaLedger.GetUsageAsync(cancellationToken);

        var notes = new List<string>
        {
            "Results are YouTube's search ranking for this query, not search volume. YouTube doesn't publish search volume.",
            "Stats come from a separate videos.list call (1 unit).",
        };
        if (result.FromCache)
        {
            notes.Add($"Search results came from the cache (saved {result.RetrievedAtUtc:u}); the search itself cost 0 units.");
        }

        return new SearchReport(
            result.Request.Query,
            result.Request.Order.ToString(),
            result.FromCache,
            result.TotalResultsEstimate,
            items,
            new QuotaSnapshot(usage.UnitsUsed, usage.UnitsRemaining, usage.SearchCallsRemaining, usage.QuotaDayResetsAtUtc),
            notes);
    }

    public async Task<ChannelReport> GetChannelAsync(string channel, CancellationToken cancellationToken)
    {
        var data = await loader.GetChannelAsync(channel, cancellationToken);
        await loader.RecordChannelAsync(data, cancellationToken);
        var stored = await channelStore.FindAsync(data.Id, cancellationToken);
        var history = stored is null ? [] : await snapshots.GetChannelSnapshotsAsync(data.Id, null, cancellationToken);

        var notes = new List<string>();
        if (data.SubscriberCountHidden)
        {
            notes.Add("The channel hides its subscriber count.");
        }
        else
        {
            notes.Add("YouTube rounds subscriber counts to 3 significant figures.");
        }

        return new ChannelReport(
            ChannelVideoLoader.Summarize(data),
            Truncate(data.Description),
            data.PublishedAtUtc,
            data.Country,
            new ChannelStats(data.SubscriberCount, data.SubscriberCountHidden, data.ViewCount, data.VideoCount, data.RetrievedAtUtc),
            stored?.IsTracked ?? false,
            history.Count,
            notes);
    }

    public async Task<ChannelVideosReport> GetChannelVideosAsync(
        string channel, int maxResults, ChannelVideoOrder order, VideoFormat? format, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maxResults, 1, 50);
        var data = await loader.GetChannelAsync(channel, cancellationToken);

        // The uploads playlist is newest-first with no popularity sort; "popular" ranks a wider recent scan locally.
        var scan = order == ChannelVideoOrder.Popular || format is not null ? PopularScan : limit;
        var uploads = await loader.GetRecentUploadsAsync(data, scan, cancellationToken);
        await snapshots.AddVideoSnapshotsAsync(uploads, cancellationToken);
        var shortsMax = options.Value.ShortsMaxDurationSeconds;

        var filtered = uploads.Where(u => format is null || VideoFormatClassifier.Classify(u.Duration, shortsMax) == format);
        var ordered = order == ChannelVideoOrder.Popular
            ? filtered.OrderByDescending(u => u.ViewCount ?? -1)
            : filtered.OrderByDescending(u => u.PublishedAtUtc);
        var tracked = (await videoStore.ListByChannelAsync(data.Id, cancellationToken)).Where(v => v.IsTracked).Select(v => v.YouTubeVideoId).ToHashSet();

        var notes = new List<string>();
        if (order == ChannelVideoOrder.Popular)
        {
            notes.Add($"'Popular' = most viewed among the channel's latest {uploads.Count} uploads (lifetime views). The API can't sort a channel's whole catalogue by popularity without an expensive search.");
        }

        if (format is not null)
        {
            notes.Add($"Filtered to {(format == VideoFormat.Short ? "Shorts (180 s or less)" : "long-form videos (over 180 s)")} among the latest {uploads.Count} uploads.");
        }

        return new ChannelVideosReport(
            ChannelVideoLoader.Summarize(data),
            order,
            format,
            uploads.Count,
            ordered.Take(limit).Select(u => Item(u, tracked.Contains(u.Id))).ToList(),
            notes);
    }

    public async Task<VideoReport> GetVideoAsync(string video, CancellationToken cancellationToken)
    {
        var id = YouTubeInput.ParseVideoId(video);
        var batch = await youTube.GetVideosAsync([id], cancellationToken);
        var data = batch.Videos.SingleOrDefault()
            ?? throw new YouTubeApiException(YouTubeErrorKind.NotFound, $"Video {id} wasn't found or isn't public.");
        await snapshots.AddVideoSnapshotsAsync([data], cancellationToken);
        var stored = await videoStore.FindAsync(id, cancellationToken);

        return new VideoReport(
            loader.Summarize(data, stored?.IsTracked ?? false),
            Truncate(data.Description),
            data.Tags,
            new YouTubeVideoStats(data.ViewCount, data.LikeCount, data.CommentCount, data.RetrievedAtUtc),
            [
                LifetimeAverage.ViewsPerDay(data.ViewCount, data.PublishedAtUtc, data.RetrievedAtUtc),
                EngagementRates.LikeRate(data.LikeCount, data.ViewCount),
                EngagementRates.CommentRate(data.CommentCount, data.ViewCount),
            ],
            ["For velocity, history and an outlier score, use analyze_video.", Limitations.Calculated]);
    }

    private VideoListItem Item(VideoData v, bool tracked) => new(
        loader.Summarize(v, tracked),
        new YouTubeVideoStats(v.ViewCount, v.LikeCount, v.CommentCount, v.RetrievedAtUtc),
        LifetimeAverage.ViewsPerDay(v.ViewCount, v.PublishedAtUtc, v.RetrievedAtUtc));

    private static string Truncate(string text) =>
        text.Length <= MaxDescriptionLength ? text : text[..MaxDescriptionLength] + "…";
}
