using PersonalVidIQ.Application.Analytics;

namespace PersonalVidIQ.Application.Research;

/// <summary>
/// Emerging keywords across tracked channels. A keyword is reported when, in the recent window, it appears in
/// at least MinVideos videos and either (a) is used noticeably more than in the window before ("more_uploads"),
/// or (b) its recent videos beat their channels' baselines ("outperforming"), or both. More channels and more
/// scored videos raise confidence. Never "trending" from a single video. DECISIONS.md, brief "Trend detection".
/// </summary>
public static class TrendDetector
{
    public const string Formula =
        "recent window vs the window before it: count of videos using the keyword, distinct channels, and median channel-relative ratio of the recent ones";

    public static IReadOnlyList<EmergingTerm> Detect(
        IReadOnlyDictionary<string, List<ResearchVideo>> index,
        DateTimeOffset nowUtc,
        int windowDays,
        int minVideos,
        int trackedChannelCount)
    {
        var recentStart = nowUtc.AddDays(-windowDays);
        var priorStart = nowUtc.AddDays(-2 * windowDays);
        var minChannels = trackedChannelCount >= 2 ? 2 : 1;
        var results = new List<EmergingTerm>();

        foreach (var (term, videos) in index)
        {
            var recent = videos.Where(v => v.Input.PublishedAtUtc >= recentStart).ToList();
            if (recent.Count < minVideos)
            {
                continue;
            }

            var prior = videos.Count(v => v.Input.PublishedAtUtc >= priorStart && v.Input.PublishedAtUtc < recentStart);
            var channels = recent.Select(v => v.ChannelId).Distinct().Count();
            if (channels < minChannels)
            {
                continue;
            }

            var ratios = recent.Where(v => v.ChannelRelativeRatio is not null).Select(v => v.ChannelRelativeRatio!.Value).ToList();
            var median = Statistics.Median(ratios);
            var moreUploads = recent.Count >= 2 * Math.Max(1, prior);
            var outperforming = median >= 1.5 && ratios.Count >= minVideos;
            if (!moreUploads && !outperforming)
            {
                continue;
            }

            var signal = (moreUploads, outperforming) switch
            {
                (true, true) => "more_uploads_and_outperforming",
                (true, false) => "more_uploads",
                _ => "outperforming",
            };

            var confidence = channels >= 3 && ratios.Count >= 5 ? Confidence.Medium : Confidence.Low;
            results.Add(new EmergingTerm(
                term,
                recent.Count,
                prior,
                channels,
                median is { } m ? Statistics.Round(m) : null,
                signal,
                confidence,
                recent.OrderByDescending(v => v.ChannelRelativeRatio ?? 0).Take(3).Select(Evidence.From).ToList()));
        }

        return results
            .OrderByDescending(r => r.Signal == "more_uploads_and_outperforming")
            .ThenByDescending(r => r.RecentChannels)
            .ThenByDescending(r => r.RecentMedianChannelRelativeRatio ?? 0)
            .ThenBy(r => r.Term, StringComparer.Ordinal)
            .ToList();
    }
}
