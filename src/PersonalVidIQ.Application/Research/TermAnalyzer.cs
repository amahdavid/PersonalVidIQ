using PersonalVidIQ.Application.Analytics;

namespace PersonalVidIQ.Application.Research;

/// <summary>
/// Keyword performance across videos. A keyword's score is the median channel-relative ratio of the videos that
/// use it (each video's views/day ÷ its own channel's same-format, similar-age median), so big and small channels,
/// Shorts and long-form are judged on the same footing. Not search volume.
/// </summary>
public static class TermAnalyzer
{
    public const string Formula =
        "median, over videos using the keyword, of (video views/day ÷ median views/day of the same channel's same-format videos of similar age)";

    public static Dictionary<string, List<ResearchVideo>> Index(IEnumerable<ResearchVideo> videos, TermExtractor extractor)
    {
        var index = new Dictionary<string, List<ResearchVideo>>(StringComparer.Ordinal);
        foreach (var video in videos)
        {
            foreach (var term in extractor.Extract(video.Input.Title, video.Tags))
            {
                if (!index.TryGetValue(term, out var list))
                {
                    index[term] = list = [];
                }

                list.Add(video);
            }
        }

        return index;
    }

    /// <summary>
    /// Drops single words that only ever appear inside one phrase ("air" and "fryer" when every such video says
    /// "air fryer"), so the phrase is reported once instead of three times.
    /// </summary>
    public static Dictionary<string, List<ResearchVideo>> RemoveSubsumed(Dictionary<string, List<ResearchVideo>> index)
    {
        var redundant = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (phrase, phraseVideos) in index.Where(kv => kv.Key.Contains(' ', StringComparison.Ordinal)))
        {
            var phraseIds = phraseVideos.Select(v => v.Input.VideoId).ToHashSet(StringComparer.Ordinal);
            foreach (var word in phrase.Split(' '))
            {
                if (index.TryGetValue(word, out var wordVideos) && wordVideos.Count == phraseIds.Count
                    && wordVideos.All(v => phraseIds.Contains(v.Input.VideoId)))
                {
                    redundant.Add(word);
                }
            }
        }

        return index.Where(kv => !redundant.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    public static TermStat Stat(string term, IReadOnlyList<ResearchVideo> videos)
    {
        var ratios = videos.Where(v => v.ChannelRelativeRatio is not null).Select(v => v.ChannelRelativeRatio!.Value).ToList();
        var perDay = videos.Where(v => v.LifetimeViewsPerDay is not null).Select(v => v.LifetimeViewsPerDay!.Value).ToList();
        var channels = videos.Select(v => v.ChannelId).Distinct().Count();

        return new TermStat(
            term,
            videos.Count,
            channels,
            ratios.Count,
            Statistics.Median(ratios) is { } r ? Statistics.Round(r) : null,
            Statistics.Median(perDay) is { } d ? Statistics.Round(d) : null,
            ConfidenceFor(ratios.Count, channels),
            videos.OrderByDescending(v => v.ChannelRelativeRatio ?? 0).Take(3).Select(Evidence.From).ToList());
    }

    /// <summary>Keyword signals are noisy; they top out at medium.</summary>
    public static Confidence ConfidenceFor(int scoredVideos, int channels) => scoredVideos switch
    {
        >= 8 when channels >= 2 => Confidence.Medium,
        >= 3 => Confidence.Low,
        _ => Confidence.Unavailable,
    };
}
