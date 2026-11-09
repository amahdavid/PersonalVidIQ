using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.Research;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Services;

/// <summary>Keyword research from official API data only. Never claims search volume. DECISIONS.md #10.</summary>
public sealed class KeywordResearchService(
    IYouTubeDataClient youTube,
    IQuotaLedger quotaLedger,
    ResearchDataLoader dataLoader,
    ChannelVideoLoader videoLoader,
    IOptions<ResearchOptions> researchOptions,
    IOptions<DataCollectionOptions> collectionOptions,
    TimeProvider timeProvider) : IKeywordResearchService
{
    public const string NotSearchVolume =
        "YouTube doesn't publish search volume, and nothing here estimates it. These are signals from videos, not demand figures.";

    private ResearchOptions Options => researchOptions.Value;

    public async Task<KeywordReport> ResearchKeywordAsync(string keyword, int? maxResults, string? regionCode, CancellationToken cancellationToken)
    {
        var extractor = new TermExtractor(Options.ExtraStopWords);
        var search = await youTube.SearchVideosAsync(new SearchRequest(keyword, maxResults, RegionCode: regionCode), cancellationToken);
        var stats = await youTube.GetVideosAsync(search.Hits.Select(h => h.VideoId).ToList(), cancellationToken);
        var videos = search.Hits.Select(h => stats.Videos.FirstOrDefault(v => v.Id == h.VideoId)).OfType<VideoData>().ToList();
        var channels = await youTube.GetChannelsAsync(videos.Select(v => v.ChannelId).Distinct().ToList(), cancellationToken);
        var subscribers = channels.ToDictionary(c => c.Id, c => c.SubscriberCount, StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();
        var shortsMax = collectionOptions.Value.ShortsMaxDurationSeconds;
        var keywordTerms = extractor.Extract(keyword);
        var window = $"top {videos.Count} YouTube search results for \"{search.Request.Query}\"";

        var competition = new List<CalculatedMetric>
        {
            MedianOf("median_views", videos.Where(v => v.ViewCount is not null).Select(v => (double)v.ViewCount!.Value), "views", window),
            MedianOf("median_lifetime_views_per_day",
                videos.Where(v => v.ViewCount is not null && v.PublishedAtUtc is not null)
                    .Select(v => v.ViewCount!.Value / LifetimeAverage.AgeDays(v.PublishedAtUtc!.Value, now)),
                "views/day", window),
            Share("share_published_last_30_days", videos, v => v.PublishedAtUtc >= now.AddDays(-30), window,
                "High = YouTube is surfacing fresh videos for this keyword."),
            Share("share_shorts", videos, v => VideoFormatClassifier.Classify(v.Duration, shortsMax) == VideoFormat.Short, window, null),
            Share("share_with_keyword_in_title", videos,
                v => keywordTerms.Count > 0 && keywordTerms.All(t => extractor.Extract(v.Title, v.Tags).Contains(t)), window,
                "Low = few results target this exact wording."),
            new("distinct_channels", videos.Select(v => v.ChannelId).Distinct().Count(), "channels",
                videos.Count >= 10 ? Confidence.Medium : Confidence.Low, "count of distinct channels in results", window, videos.Count,
                ["Few distinct channels = a handful of channels dominate this keyword."]),
            MedianOf("median_channel_subscribers",
                videos.Select(v => subscribers.GetValueOrDefault(v.ChannelId)).Where(s => s is not null).Select(s => (double)s!.Value),
                "subscribers", window),
            Share($"share_from_channels_under_{Options.SmallChannelSubscribers}_subscribers", videos,
                v => subscribers.GetValueOrDefault(v.ChannelId) is { } s && s < Options.SmallChannelSubscribers, window,
                "High = smaller channels can rank here."),
        };

        var related = videos
            .SelectMany(v => extractor.Extract(v.Title, v.Tags))
            .Where(t => !keywordTerms.Contains(t))
            .GroupBy(t => t)
            .Select(g => new RelatedTerm(g.Key, g.Count()))
            .Where(r => r.Results >= 2)
            .OrderByDescending(r => r.Results)
            .ThenBy(r => r.Term, StringComparer.Ordinal)
            .Take(Options.MaxTermsReturned)
            .ToList();

        var usage = await quotaLedger.GetUsageAsync(cancellationToken);
        var notes = new List<string>
        {
            NotSearchVolume,
            "YouTube's total-results estimate is a rough index count, not demand.",
            "Search ranking varies by viewer, region and time; these results are one snapshot.",
            CalculatedMetric.Disclaimer,
        };
        if (search.FromCache)
        {
            notes.Add($"Search results came from the cache (saved {search.RetrievedAtUtc:u}); the search cost 0 units, stats cost 2.");
        }

        return new KeywordReport(
            search.Request.Query,
            search.FromCache,
            search.TotalResultsEstimate,
            videos.Count,
            competition,
            related,
            videos.Take(10).Select(v => new VideoListItem(
                videoLoader.Summarize(v, false),
                new YouTubeVideoStats(v.ViewCount, v.LikeCount, v.CommentCount, v.RetrievedAtUtc),
                LifetimeAverage.ViewsPerDay(v.ViewCount, v.PublishedAtUtc, v.RetrievedAtUtc))).ToList(),
            new QuotaSnapshot(usage.UnitsUsed, usage.UnitsRemaining, usage.SearchCallsRemaining, usage.QuotaDayResetsAtUtc),
            notes);
    }

    public async Task<KeywordIdeasReport> GetKeywordIdeasAsync(string? seed, VideoFormat? format, CancellationToken cancellationToken)
    {
        var extractor = new TermExtractor(Options.ExtraStopWords);
        var dataset = await dataLoader.LoadAsync(null, cancellationToken);
        var videos = dataset.Videos.Where(v => format is null || v.Format == format).ToList();
        var notes = ResearchNotes.ForDataset(dataset, timeProvider.GetUtcNow());

        string? normalisedSeed = null;
        if (!string.IsNullOrWhiteSpace(seed))
        {
            normalisedSeed = TermExtractor.Normalise(seed);
            var seedTerms = extractor.Extract(seed);
            videos = videos.Where(v =>
                    TermExtractor.Normalise(v.Input.Title).Contains(normalisedSeed, StringComparison.Ordinal)
                    || (seedTerms.Count > 0 && seedTerms.All(t => extractor.Extract(v.Input.Title, v.Tags).Contains(t))))
                .ToList();
            notes.Add($"Limited to {videos.Count} stored videos whose title or tags contain \"{normalisedSeed}\"; keywords listed are the ones used alongside it.");
        }

        var index = TermAnalyzer.Index(videos, extractor);
        if (normalisedSeed is not null)
        {
            foreach (var t in extractor.Extract(seed!).Append(normalisedSeed))
            {
                index.Remove(t);
            }
        }

        var eligible = TermAnalyzer.RemoveSubsumed(index.Where(kv => kv.Value.Count >= Options.MinVideosPerTerm).ToDictionary(kv => kv.Key, kv => kv.Value));
        var stats = eligible.Select(kv => TermAnalyzer.Stat(kv.Key, kv.Value)).ToList();

        var best = stats.Where(s => s.MedianChannelRelativeRatio is not null && s.Confidence != Confidence.Unavailable)
            .OrderByDescending(s => s.MedianChannelRelativeRatio)
            .ThenByDescending(s => s.Videos)
            .Take(Options.MaxTermsReturned)
            .ToList();
        var mostUsed = stats.OrderByDescending(s => s.Videos).ThenByDescending(s => s.Channels).Take(Options.MaxTermsReturned).ToList();
        var topics = TopicGrouper.Group(eligible, eligible.Keys.ToList(), Options.TopicOverlapThreshold, Options.MaxTermsReturned);

        notes.Add(NotSearchVolume);
        notes.Add("A keyword's score shows how videos using it did compared with their own channel's usual; 1.0 = typical. It's a correlation, not proof the keyword caused it.");
        if (eligible.Count == 0)
        {
            notes.Add($"No keyword appears in at least {Options.MinVideosPerTerm} stored videos yet. Track more channels or refresh_tracked to build data.");
        }

        return new KeywordIdeasReport(
            normalisedSeed,
            format,
            Scope(dataset, videos.Count),
            best,
            mostUsed,
            topics,
            TermAnalyzer.Formula,
            notes);
    }

    internal static DataScope Scope(ResearchDataset dataset, int videos) =>
        new("stored videos of tracked channels", dataset.Channels, videos, dataset.VideosWithoutSnapshots, dataset.OldestDataUtc, dataset.NewestDataUtc);

    private static CalculatedMetric MedianOf(string name, IEnumerable<double> values, string unit, string window)
    {
        var list = values.ToList();
        return list.Count == 0
            ? CalculatedMetric.Unavailable(name, unit, "median over search results", "No results with this data.")
            : new CalculatedMetric(name, Statistics.Round(Statistics.Median(list)!.Value), unit,
                list.Count >= 10 ? Confidence.Medium : Confidence.Low, "median over search results", window, list.Count, []);
    }

    private static CalculatedMetric Share(string name, List<VideoData> videos, Func<VideoData, bool> predicate, string window, string? note) =>
        videos.Count == 0
            ? CalculatedMetric.Unavailable(name, "%", "matching results ÷ results", "No results.")
            : new CalculatedMetric(name, Statistics.Round(100.0 * videos.Count(predicate) / videos.Count, 1), "%",
                videos.Count >= 10 ? Confidence.Medium : Confidence.Low, "matching results ÷ results", window, videos.Count,
                note is null ? [] : [note]);
}

internal static class ResearchNotes
{
    public static List<string> ForDataset(ResearchDataset dataset, DateTimeOffset now)
    {
        var notes = new List<string>();
        if (dataset.Channels == 0)
        {
            notes.Add("No channels are tracked, so there's nothing to analyse. Use track_channel on channels in your niche (including your own), then refresh_tracked.");
            return notes;
        }

        notes.Add($"Based only on stored videos of {dataset.Channels} tracked channel{(dataset.Channels == 1 ? "" : "s")}; results are only as broad as what you track.");
        if (dataset.NewestDataUtc is { } newest && now - newest > TimeSpan.FromHours(24))
        {
            notes.Add($"Newest stored stats are from {newest:u}. Run refresh_tracked for fresher numbers.");
        }

        if (dataset.VideosWithoutSnapshots > 0)
        {
            notes.Add($"{dataset.VideosWithoutSnapshots} stored videos have no stats in the last 30 days and only count towards keyword frequency.");
        }

        return notes;
    }
}
