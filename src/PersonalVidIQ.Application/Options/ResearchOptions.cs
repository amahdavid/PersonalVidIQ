using System.ComponentModel.DataAnnotations;

namespace PersonalVidIQ.Application.Options;

public sealed class ResearchOptions
{
    public const string SectionName = "Research";

    /// <summary>
    /// Extra words to ignore when extracting keywords, on top of the built-in English function words and
    /// YouTube boilerplate. Add words for other languages or niche filler here; nothing niche-specific is built in.
    /// </summary>
    public List<string> ExtraStopWords { get; set; } = [];

    /// <summary>A keyword needs at least this many videos to be reported.</summary>
    [Range(2, 100)]
    public int MinVideosPerTerm { get; set; } = 3;

    [Range(5, 100)]
    public int MaxTermsReturned { get; set; } = 25;

    /// <summary>Channels below this many subscribers count as "small" in keyword competition stats.</summary>
    [Range(0, 100_000_000)]
    public long SmallChannelSubscribers { get; set; } = 10_000;

    /// <summary>Two keywords belong to one topic when the videos using them overlap this much (Jaccard).</summary>
    [Range(0.1, 1.0)]
    public double TopicOverlapThreshold { get; set; } = 0.5;
}
