using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.Research;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Services;

public sealed class TrendResearchService(
    ResearchDataLoader dataLoader,
    IChannelStore channelStore,
    ChannelVideoLoader videoLoader,
    IOptions<ResearchOptions> researchOptions,
    TimeProvider timeProvider) : ITrendResearchService
{
    private ResearchOptions Options => researchOptions.Value;

    public async Task<TrendReport> DiscoverTrendsAsync(int windowDays, VideoFormat? format, CancellationToken cancellationToken)
    {
        if (windowDays is < 1 or > 15)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, "windowDays must be 1-15 (the window is compared with the one before it, and history is kept 30 days).");
        }

        var now = timeProvider.GetUtcNow();
        var dataset = await dataLoader.LoadAsync(null, cancellationToken);
        var videos = dataset.Videos.Where(v => format is null || v.Format == format).ToList();
        var index = TermAnalyzer.RemoveSubsumed(TermAnalyzer.Index(videos, new TermExtractor(Options.ExtraStopWords)));
        var emerging = TrendDetector.Detect(index, now, windowDays, Options.MinVideosPerTerm, dataset.Channels).Take(Options.MaxTermsReturned).ToList();

        var notes = ResearchNotes.ForDataset(dataset, now);
        notes.Add("Emerging = used noticeably more than in the previous window, and/or recent videos using it beat their channels' usual. Not a claim about YouTube-wide search or demand.");
        if (dataset.Channels == 1)
        {
            notes.Add("Only one channel is tracked, so these reflect that channel's own choices, not a niche-wide trend.");
        }

        if (dataset.Channels > 0 && emerging.Count == 0)
        {
            notes.Add($"Nothing qualifies yet: a keyword needs {Options.MinVideosPerTerm}+ recent videos{(dataset.Channels >= 2 ? " from 2+ channels" : string.Empty)}.");
        }

        return new TrendReport(windowDays, format, KeywordResearchService.Scope(dataset, videos.Count), emerging, TrendDetector.Formula, notes);
    }

    public async Task<RecentUploadsReport> GetRecentUploadsAsync(int hours, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromHours(Math.Clamp(hours, 1, 720));
        var now = timeProvider.GetUtcNow();
        var dataset = await dataLoader.LoadAsync(null, cancellationToken);
        var recent = dataset.Videos
            .Where(v => v.Input.PublishedAtUtc >= now - window)
            .OrderByDescending(v => v.Input.PublishedAtUtc)
            .Select(v => new RecentUpload(
                Evidence.From(v),
                ViewVelocity.Calculate(v.Input.AllObservations.Where(o => o.Views is not null && o.AtUtc <= v.Input.CapturedAtUtc), TimeSpan.FromHours(24), "views_per_day_last_24h").Metric,
                v.Input.PublishedAtUtc is { } p ? Statistics.Round((now - p).TotalHours, 1) : null))
            .ToList();

        var notes = ResearchNotes.ForDataset(dataset, now);
        notes.Add("Uploads appear here once refresh_tracked (or track_channel) has seen them; run refresh_tracked first for the latest.");
        notes.Add(CalculatedMetric.Disclaimer);
        return new RecentUploadsReport((int)window.TotalHours, KeywordResearchService.Scope(dataset, recent.Count), recent, notes);
    }

    public async Task<IdeaEvidenceReport> GetVideoIdeaEvidenceAsync(string? myChannel, int windowDays, CancellationToken cancellationToken)
    {
        if (windowDays is < 1 or > 15)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, "windowDays must be 1-15.");
        }

        var now = timeProvider.GetUtcNow();
        var extractor = new TermExtractor(Options.ExtraStopWords);
        var dataset = await dataLoader.LoadAsync(null, cancellationToken);
        var notes = ResearchNotes.ForDataset(dataset, now);

        // Resolve "my channel" so its own videos can be excluded from competitor evidence and used for gap analysis.
        string? myChannelId = null;
        string? myChannelTitle = null;
        IReadOnlySet<string> myTerms = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(myChannel))
        {
            var parsed = YouTubeInput.ParseChannel(myChannel);
            var stored = parsed.Kind switch
            {
                ChannelRefKind.Id => await channelStore.FindAsync(parsed.Value, cancellationToken),
                ChannelRefKind.Handle => await channelStore.FindByHandleAsync(parsed.Value, cancellationToken),
                _ => null,
            };

            if (stored is not null)
            {
                myChannelId = stored.YouTubeChannelId;
                myChannelTitle = stored.Title;
                var mine = (await dataLoader.LoadAsync([stored.YouTubeChannelId], cancellationToken)).Videos;
                myTerms = mine.SelectMany(v => extractor.Extract(v.Input.Title, v.Tags)).ToHashSet(StringComparer.Ordinal);
            }
            else
            {
                var data = await videoLoader.GetChannelAsync(myChannel, cancellationToken);
                myChannelId = data.Id;
                myChannelTitle = data.Title;
                var uploads = await videoLoader.GetRecentUploadsAsync(data, 50, cancellationToken);
                myTerms = uploads.SelectMany(v => extractor.Extract(v.Title, v.Tags)).ToHashSet(StringComparer.Ordinal);
                notes.Add($"{data.Title} isn't stored, so its latest {uploads.Count} uploads were fetched for comparison (about 2 quota units). Track it for richer comparisons.");
            }
        }

        var competitors = dataset.Videos.Where(v => v.ChannelId != myChannelId).ToList();
        var index = TermAnalyzer.Index(competitors, extractor);
        var eligible = TermAnalyzer.RemoveSubsumed(index.Where(kv => kv.Value.Count >= Options.MinVideosPerTerm).ToDictionary(kv => kv.Key, kv => kv.Value));
        var stats = eligible.Select(kv => TermAnalyzer.Stat(kv.Key, kv.Value)).ToList();
        var competitorChannels = competitors.Select(v => v.ChannelId).Distinct().Count();

        var outliers = competitors
            .Where(v => v.Input.PublishedAtUtc >= now.AddDays(-30) && v.ChannelRelativeRatio >= 2)
            .OrderByDescending(v => v.ChannelRelativeRatio)
            .Take(10)
            .Select(Evidence.From)
            .ToList();

        var best = stats.Where(s => s.MedianChannelRelativeRatio >= 1.2 && s.Confidence != Confidence.Unavailable)
            .OrderByDescending(s => s.MedianChannelRelativeRatio).Take(10).ToList();

        var emerging = TrendDetector.Detect(TermAnalyzer.RemoveSubsumed(index), now, windowDays, Options.MinVideosPerTerm, competitorChannels).Take(10).ToList();

        var gaps = myChannelId is null
            ? []
            : stats.Where(s => s.Channels >= Math.Min(2, competitorChannels) && s.MedianChannelRelativeRatio >= 1.2 && !myTerms.Contains(s.Term))
                .OrderByDescending(s => s.MedianChannelRelativeRatio)
                .Take(10)
                .Select(s => new KeywordGap(s.Term, s.Videos, s.Channels, s.MedianChannelRelativeRatio, s.Examples))
                .ToList();

        if (myChannelId is not null && competitorChannels == 0)
        {
            notes.Add("No competitor channels are tracked, so there's nothing to compare your channel against.");
        }

        return new IdeaEvidenceReport(
            myChannelTitle,
            windowDays,
            KeywordResearchService.Scope(dataset, competitors.Count),
            outliers,
            best,
            emerging,
            gaps,
            [
                "Propose ideas that are grounded in this evidence, and cite the specific videos or keywords behind each one.",
                "Channel-relative ratio: 2.0 = a video did twice its own channel's usual for its format and age.",
                "Keyword gaps are keywords that do well for tracked competitors but don't appear in your channel's titles or tags.",
                "These are correlations from public data; they don't show why a video performed (thumbnails, timing and promotion aren't visible).",
            ],
            notes);
    }
}
