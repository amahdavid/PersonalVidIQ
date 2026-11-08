using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Services;

public sealed class ChannelResearchService(
    ChannelVideoLoader loader,
    IChannelStore channelStore,
    ISnapshotStore snapshots,
    IOptions<DataCollectionOptions> options,
    TimeProvider timeProvider) : IChannelResearchService
{
    public const int MaxChannels = 5;
    private const int UploadsToCheck = 50;

    public async Task<ChannelComparison> CompareChannelsAsync(IReadOnlyList<string> channels, int periodDays, CancellationToken cancellationToken)
    {
        if (channels.Count == 0 || channels.Count > MaxChannels)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, $"Compare between 1 and {MaxChannels} channels.");
        }

        var retention = options.Value.SnapshotRetentionDays;
        if (periodDays < 1 || periodDays > retention)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, $"periodDays must be between 1 and {retention} (history is kept for {retention} days).");
        }

        var refs = channels.Select(YouTubeInput.ParseChannel).Distinct().ToList();
        var now = timeProvider.GetUtcNow();
        var entries = new List<ChannelComparisonEntry>();
        var notFound = new List<string>();

        foreach (var channelRef in refs)
        {
            ChannelData channel;
            try
            {
                channel = await loader.GetChannelAsync(channelRef.Value, cancellationToken);
            }
            catch (YouTubeApiException ex) when (ex.Kind == YouTubeErrorKind.NotFound)
            {
                notFound.Add(channelRef.Value);
                continue;
            }

            // History is read before the fresh read is stored, then the fresh read is added as the latest point.
            var history = await snapshots.GetChannelSnapshotsAsync(channel.Id, now.AddDays(-periodDays), cancellationToken);
            await loader.RecordChannelAsync(channel, cancellationToken);
            var uploads = await loader.GetRecentUploadsAsync(channel, UploadsToCheck, cancellationToken);
            await loader.BuildInputsAsync(uploads, cancellationToken);
            var stored = await channelStore.FindAsync(channel.Id, cancellationToken);

            entries.Add(new ChannelComparisonEntry(
                ChannelVideoLoader.Summarize(channel),
                new ChannelStats(channel.SubscriberCount, channel.SubscriberCountHidden, channel.ViewCount, channel.VideoCount, channel.RetrievedAtUtc),
                Activity(uploads, periodDays, now),
                Growth(channel, history.Select(h => (h.CapturedAtUtc, h.ViewCount, h.SubscriberCount)).ToList()),
                stored?.IsTracked ?? false));
        }

        return new ChannelComparison(entries, notFound, now,
        [
            Limitations.Calculated,
            Limitations.NoPrivateMetrics,
            $"Activity looks at each channel's latest {UploadsToCheck} uploads.",
            "Subscriber counts are rounded by YouTube to 3 significant figures, so subscriber change is low confidence.",
            "Growth needs earlier snapshots: it's only available for channels PersonalVidIQ has stored and refreshed before.",
        ]);
    }

    private ChannelPeriodActivity Activity(IReadOnlyList<VideoData> uploads, int periodDays, DateTimeOffset now)
    {
        var cutoff = now.AddDays(-periodDays);
        var inPeriod = uploads.Where(u => u.PublishedAtUtc >= cutoff && u.LiveBroadcastContent is not ("live" or "upcoming")).ToList();
        var shortsMax = options.Value.ShortsMaxDurationSeconds;
        var shorts = inPeriod.Where(u => VideoFormatClassifier.Classify(u.Duration, shortsMax) == VideoFormat.Short).ToList();
        var longForm = inPeriod.Where(u => VideoFormatClassifier.Classify(u.Duration, shortsMax) == VideoFormat.LongForm).ToList();
        var window = $"published in the last {periodDays} days";
        var coversPeriod = uploads.Count < UploadsToCheck || uploads.Min(u => u.PublishedAtUtc) <= cutoff;

        var uploadNotes = coversPeriod ? new List<string>() : [$"The latest {UploadsToCheck} uploads don't reach back {periodDays} days; the true count is higher."];
        return new ChannelPeriodActivity(
            periodDays,
            uploads.Count,
            inPeriod.Count,
            shorts.Count,
            longForm.Count,
            MedianViewsPerDay("median_short_views_per_day", shorts, now, window),
            MedianViewsPerDay("median_long_form_views_per_day", longForm, now, window),
            new CalculatedMetric("uploads_per_week", Statistics.Round(inPeriod.Count / (periodDays / 7.0)), "uploads/week",
                coversPeriod ? Confidence.High : Confidence.Low, "uploads in period ÷ (period days ÷ 7)", window, inPeriod.Count, uploadNotes));
    }

    private static CalculatedMetric MedianViewsPerDay(string name, List<VideoData> videos, DateTimeOffset now, string window)
    {
        const string formula = "median over videos of (views ÷ days since publish)";
        var values = videos.Where(v => v.ViewCount is not null && v.PublishedAtUtc is not null)
            .Select(v => v.ViewCount!.Value / LifetimeAverage.AgeDays(v.PublishedAtUtc!.Value, now))
            .ToList();
        if (values.Count == 0)
        {
            return CalculatedMetric.Unavailable(name, "views/day", formula, "No videos of this format in the period.");
        }

        var confidence = values.Count >= 5 ? Confidence.Medium : Confidence.Low;
        var notes = new List<string> { "Lifetime average per video, so newer videos in the period weigh in differently from older ones." };
        if (values.Count < 5)
        {
            notes.Add($"Only {values.Count} videos.");
        }

        return new CalculatedMetric(name, Statistics.Round(Statistics.Median(values)!.Value), "views/day", confidence, formula, window, values.Count, notes);
    }

    private static ChannelGrowth Growth(ChannelData current, List<(DateTimeOffset At, long? Views, long? Subscribers)> history)
    {
        var viewObservations = history.Select(h => new Observation(h.At, h.Views)).Append(new Observation(current.RetrievedAtUtc, current.ViewCount));
        var views = ViewVelocity.Calculate(viewObservations, null, "channel_total_views_per_day").Metric;

        const string subFormula = "latest subscriber count − earliest in period";
        CalculatedMetric subscribers;
        var earliest = history.Where(h => h.Subscribers is not null).OrderBy(h => h.At).FirstOrDefault();
        if (current.SubscriberCount is null)
        {
            subscribers = CalculatedMetric.Unavailable("subscriber_change", "subscribers", subFormula, "Subscriber count is hidden.");
        }
        else if (earliest.Subscribers is null)
        {
            subscribers = CalculatedMetric.Unavailable("subscriber_change", "subscribers", subFormula, "No earlier snapshot in the period.");
        }
        else
        {
            subscribers = new CalculatedMetric(
                "subscriber_change",
                current.SubscriberCount.Value - earliest.Subscribers.Value,
                "subscribers",
                Confidence.Low,
                subFormula,
                $"{earliest.At:u} → {current.RetrievedAtUtc:u}",
                2,
                ["YouTube rounds subscriber counts to 3 significant figures; small changes may not show and steps are coarse."]);
        }

        return new ChannelGrowth(history.Count, views, subscribers);
    }
}
