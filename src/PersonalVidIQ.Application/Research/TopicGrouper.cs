using PersonalVidIQ.Application.Analytics;

namespace PersonalVidIQ.Application.Research;

/// <summary>
/// Groups keywords that mostly appear on the same videos into topics: greedy, most-used keyword first,
/// joining any keyword whose video set overlaps the topic's seed by at least the Jaccard threshold.
/// </summary>
public static class TopicGrouper
{
    public static IReadOnlyList<TopicGroup> Group(
        IReadOnlyDictionary<string, List<ResearchVideo>> index, IReadOnlyCollection<string> terms, double overlapThreshold, int maxTopics)
    {
        var remaining = terms.OrderByDescending(t => index[t].Count).ThenBy(t => t, StringComparer.Ordinal).ToList();
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var topics = new List<TopicGroup>();

        foreach (var seed in remaining)
        {
            if (assigned.Contains(seed) || topics.Count >= maxTopics)
            {
                continue;
            }

            var seedSet = index[seed].Select(v => v.Input.VideoId).ToHashSet(StringComparer.Ordinal);
            var members = new List<string> { seed };
            assigned.Add(seed);
            foreach (var other in remaining.Where(t => !assigned.Contains(t)))
            {
                var otherSet = index[other].Select(v => v.Input.VideoId).ToHashSet(StringComparer.Ordinal);
                var overlap = seedSet.Intersect(otherSet).Count() / (double)seedSet.Union(otherSet).Count();
                if (overlap >= overlapThreshold)
                {
                    members.Add(other);
                    assigned.Add(other);
                }
            }

            var videos = members.SelectMany(m => index[m]).DistinctBy(v => v.Input.VideoId).ToList();
            var ratios = videos.Where(v => v.ChannelRelativeRatio is not null).Select(v => v.ChannelRelativeRatio!.Value).ToList();
            var channels = videos.Select(v => v.ChannelId).Distinct().Count();

            // Label with the longest member phrase that's still in at least half the topic's videos: more specific than the seed word.
            var label = members
                .Where(m => index[m].Count * 2 >= videos.Count)
                .OrderByDescending(m => m.Count(c => c == ' '))
                .ThenByDescending(m => index[m].Count)
                .First();

            topics.Add(new TopicGroup(
                label,
                members,
                videos.Count,
                channels,
                Statistics.Median(ratios) is { } r ? Statistics.Round(r) : null,
                TermAnalyzer.ConfidenceFor(ratios.Count, channels),
                videos.OrderByDescending(v => v.ChannelRelativeRatio ?? 0).Take(3).Select(Evidence.From).ToList()));
        }

        return topics;
    }
}
