using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Services;

public sealed class VideoAnalysisService(
    IYouTubeDataClient youTube,
    IVideoStore videoStore,
    ChannelVideoLoader loader,
    IOptions<DataCollectionOptions> options,
    TimeProvider timeProvider) : IVideoAnalysisService
{
    public const int MaxCompareVideos = 10;

    private int RetentionDays => options.Value.SnapshotRetentionDays;

    public async Task<VideoAnalysis> AnalyzeVideoAsync(string video, CancellationToken cancellationToken)
    {
        var id = YouTubeInput.ParseVideoId(video);
        var batch = await youTube.GetVideosAsync([id], cancellationToken);
        var data = batch.Videos.SingleOrDefault()
            ?? throw new YouTubeApiException(YouTubeErrorKind.NotFound, $"Video {id} wasn't found or isn't public.");

        var input = (await loader.BuildInputsAsync([data], cancellationToken))[0];
        var stored = await videoStore.FindAsync(id, cancellationToken);
        var limitations = new List<string> { Limitations.Calculated, Limitations.NoPrivateMetrics, Limitations.Retention(RetentionDays) };

        OutlierResult? outlier = null;
        if (!string.IsNullOrEmpty(data.ChannelId))
        {
            var channels = await youTube.GetChannelsAsync([data.ChannelId], cancellationToken);
            if (channels.Count == 1)
            {
                var uploads = await loader.GetRecentUploadsAsync(channels[0], options.Value.InitialUploadsToTrack, cancellationToken);
                var others = uploads.Where(u => u.Id != id).ToList();
                var cohort = await loader.BuildInputsAsync(others, cancellationToken);
                outlier = OutlierScorer.Score(input, cohort.Append(input).ToList(), loader.OutlierSettings, timeProvider.GetUtcNow());
                limitations.Add($"Outlier score compares against the channel's {uploads.Count} most recent uploads.");
            }
        }

        return Build(data, input, stored?.IsTracked ?? false, outlier, limitations);
    }

    public async Task<VideoComparison> CompareVideosAsync(IReadOnlyList<string> videos, CancellationToken cancellationToken)
    {
        if (videos.Count == 0 || videos.Count > MaxCompareVideos)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, $"Compare between 1 and {MaxCompareVideos} videos.");
        }

        var ids = videos.Select(YouTubeInput.ParseVideoId).Distinct().ToList();
        var batch = await youTube.GetVideosAsync(ids, cancellationToken);
        var inputs = await loader.BuildInputsAsync(batch.Videos, cancellationToken);
        var limitations = new List<string>
        {
            Limitations.Calculated,
            Limitations.NoPrivateMetrics,
            Limitations.Retention(RetentionDays),
            "Videos of different ages and formats aren't directly comparable on lifetime views/day; use find_outliers for channel-relative scores.",
        };

        var results = new List<VideoAnalysis>();
        foreach (var data in ids.Select(i => batch.Videos.FirstOrDefault(v => v.Id == i)).OfType<VideoData>())
        {
            var stored = await videoStore.FindAsync(data.Id, cancellationToken);
            results.Add(Build(data, inputs.First(i => i.VideoId == data.Id), stored?.IsTracked ?? false, null, []));
        }

        return new VideoComparison(results, batch.MissingIds, limitations);
    }

    public async Task<OutlierReport> FindOutliersAsync(OutlierQuery query, CancellationToken cancellationToken)
    {
        if (query.PublishedWithinDays is < 1 or > 365)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, "publishedWithinDays must be between 1 and 365.");
        }

        var normalised = query with
        {
            MaxVideos = Math.Clamp(query.MaxVideos, 10, ChannelVideoLoader.MaxUploads),
            MinRatio = Math.Max(0, query.MinRatio),
        };

        var channel = await loader.GetChannelAsync(query.Channel, cancellationToken);
        await loader.RecordChannelAsync(channel, cancellationToken);
        var uploads = await loader.GetRecentUploadsAsync(channel, normalised.MaxVideos, cancellationToken);
        var inputs = await loader.BuildInputsAsync(uploads, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-normalised.PublishedWithinDays);

        // Every upload is part of the baseline; only those in the window (and format) are reported.
        var targets = inputs
            .Where(i => i.PublishedAtUtc >= cutoff)
            .Where(i => normalised.Format is null
                || VideoFormatClassifier.Classify(i.Duration, loader.OutlierSettings.ShortsMaxDurationSeconds) == normalised.Format)
            .ToList();
        var scored = targets.Select(t => OutlierScorer.Score(t, inputs, loader.OutlierSettings, now)).ToList();

        var notes = new List<string>
        {
            Limitations.Calculated,
            $"Baseline: the channel's {uploads.Count} most recent uploads. Shorts are 180 s or less and are only compared with Shorts.",
        };
        if (uploads.Count >= normalised.MaxVideos)
        {
            notes.Add($"Only the latest {normalised.MaxVideos} uploads were checked; older videos weren't considered.");
        }

        return new OutlierReport(
            ChannelVideoLoader.Summarize(channel),
            normalised,
            uploads.Count,
            targets.Count,
            scored.Where(s => s.Ratio >= normalised.MinRatio).OrderByDescending(s => s.Ratio).ToList(),
            scored.Where(s => s.Ratio is null).ToList(),
            OutlierScorer.Formula,
            now,
            notes);
    }

    private VideoAnalysis Build(VideoData data, VideoAnalyticsInput input, bool isTracked, OutlierResult? outlier, List<string> limitations)
    {
        var observations = input.AllObservations.ToList();
        var calculated = new List<CalculatedMetric>
        {
            LifetimeAverage.ViewsPerDay(data.ViewCount, data.PublishedAtUtc, data.RetrievedAtUtc),
            ViewVelocity.Calculate(observations, TimeSpan.FromHours(24), "views_per_day_last_24h").Metric,
            ViewVelocity.Calculate(observations, TimeSpan.FromDays(7), "views_per_day_last_7d").Metric,
            EngagementRates.LikeRate(data.LikeCount, data.ViewCount),
            EngagementRates.CommentRate(data.CommentCount, data.ViewCount),
        };

        var history = new HistorySummary(
            input.History.Count,
            input.History.Count > 0 ? input.History.Min(h => h.AtUtc) : null,
            input.History.Count > 0 ? input.History.Max(h => h.AtUtc) : null,
            RetentionDays);

        if (input.History.Count == 0)
        {
            limitations.Add(isTracked
                ? "No earlier snapshots yet: velocity appears after the next refresh."
                : "This video isn't tracked, so it has no history. Track it to measure views/day over time.");
        }

        return new VideoAnalysis(
            loader.Summarize(data, isTracked),
            new YouTubeVideoStats(data.ViewCount, data.LikeCount, data.CommentCount, data.RetrievedAtUtc),
            calculated,
            outlier,
            history,
            limitations);
    }
}
