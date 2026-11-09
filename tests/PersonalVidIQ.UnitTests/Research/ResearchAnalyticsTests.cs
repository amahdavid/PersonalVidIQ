using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.Research;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.UnitTests.Research;

public sealed class ResearchAnalyticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly TermExtractor _extractor = new();

    private static ResearchVideo Video(string id, string title, string channel, double ageDays, double? ratio, string[]? tags = null) => new(
        new VideoAnalyticsInput(id, title, Now.AddDays(-ageDays), TimeSpan.FromSeconds(40), "none", 1_000, null, null, Now, []),
        channel, $"Channel {channel}", tags ?? [], VideoFormat.Short, ratio, 100);

    [Fact]
    public void Term_stat_uses_median_ratio_and_counts_channels()
    {
        var videos = new[]
        {
            Video("v1", "air fryer chicken", "A", 3, 3.0),
            Video("v2", "air fryer fries", "A", 4, 1.0),
            Video("v3", "air fryer cake", "B", 5, 2.0),
            Video("v4", "air fryer test", "B", 6, null),
        };

        var stat = TermAnalyzer.Stat("air fryer", videos);

        Assert.Equal(4, stat.Videos);
        Assert.Equal(2, stat.Channels);
        Assert.Equal(3, stat.ScoredVideos);
        Assert.Equal(2.0, stat.MedianChannelRelativeRatio);
        Assert.Equal(Confidence.Low, stat.Confidence);
        Assert.Equal("v1", stat.Examples[0].VideoId);
    }

    [Fact]
    public void Words_only_seen_inside_a_phrase_are_dropped()
    {
        var videos = new[]
        {
            Video("v1", "air fryer wings", "A", 3, 1.0),
            Video("v2", "air fryer fries", "A", 4, 1.0),
            Video("v3", "fresh air walk", "B", 5, 1.0),
        };

        var index = TermAnalyzer.RemoveSubsumed(TermAnalyzer.Index(videos, _extractor));

        Assert.Contains("air fryer", index.Keys);
        Assert.DoesNotContain("fryer", index.Keys);  // only ever in "air fryer"
        Assert.Contains("air", index.Keys);          // also used on its own
    }

    [Theory]
    [InlineData(2, 5, Confidence.Unavailable)]
    [InlineData(3, 1, Confidence.Low)]
    [InlineData(8, 1, Confidence.Low)]
    [InlineData(8, 2, Confidence.Medium)]
    public void Keyword_confidence_never_reaches_high(int scored, int channels, Confidence expected) =>
        Assert.Equal(expected, TermAnalyzer.ConfidenceFor(scored, channels));

    [Fact]
    public void Topics_group_keywords_that_share_videos()
    {
        var videos = new[]
        {
            Video("v1", "sourdough starter guide", "A", 3, 1.0),
            Video("v2", "sourdough starter mistakes", "A", 4, 1.0),
            Video("v3", "sourdough starter feeding", "B", 5, 1.0),
            Video("v4", "knife skills basics", "B", 6, 1.0),
            Video("v5", "knife skills onions", "A", 7, 1.0),
        };
        var index = TermAnalyzer.Index(videos, _extractor);
        var terms = index.Where(kv => kv.Value.Count >= 2).Select(kv => kv.Key).ToList();

        var topics = TopicGrouper.Group(index, terms, 0.5, 10);

        Assert.Equal(2, topics.Count);
        var sourdough = topics.Single(t => t.Terms.Contains("sourdough"));
        Assert.Equal("sourdough starter", sourdough.Label);
        Assert.Equal(3, sourdough.Videos);
        Assert.Contains("starter", sourdough.Terms);
        Assert.Equal(2, sourdough.Channels);
    }

    [Fact]
    public void Trends_need_several_videos_from_several_channels()
    {
        var videos = new List<ResearchVideo>
        {
            // "foldable phone": 4 recent videos across 3 channels, 0 before → more uploads.
            Video("r1", "foldable phone review", "A", 1, 1.2),
            Video("r2", "foldable phone durability", "B", 2, 2.5),
            Video("r3", "foldable phone camera", "C", 3, 1.8),
            Video("r4", "foldable phone price", "A", 5, 1.6),
            // "tablet": 3 recent from one channel only → filtered when 2+ channels tracked.
            Video("t1", "tablet unboxing", "A", 1, 5.0),
            Video("t2", "tablet games", "A", 2, 5.0),
            Video("t3", "tablet case", "A", 3, 5.0),
            // "laptop": steady (3 recent, 3 prior), typical performance → not emerging.
            Video("l1", "laptop deals", "A", 1, 1.0),
            Video("l2", "laptop deals", "B", 2, 1.0),
            Video("l3", "laptop deals", "C", 3, 1.0),
            Video("l4", "laptop deals", "A", 9, 1.0),
            Video("l5", "laptop deals", "B", 10, 1.0),
            Video("l6", "laptop deals", "C", 11, 1.0),
        };
        var index = TermAnalyzer.Index(videos, _extractor);

        var emerging = TrendDetector.Detect(index, Now, 7, 3, trackedChannelCount: 3);

        var phone = Assert.Single(emerging, e => e.Term == "foldable phone");
        Assert.Equal(4, phone.RecentVideos);
        Assert.Equal(0, phone.PriorVideos);
        Assert.Equal(3, phone.RecentChannels);
        Assert.Equal("more_uploads_and_outperforming", phone.Signal);
        Assert.DoesNotContain(emerging, e => e.Term == "tablet");
        Assert.DoesNotContain(emerging, e => e.Term.Contains("laptop", StringComparison.Ordinal));
    }

    [Fact]
    public void Single_tracked_channel_allows_single_channel_trends()
    {
        var videos = new[]
        {
            Video("t1", "tablet unboxing", "A", 1, 2.0),
            Video("t2", "tablet games", "A", 2, 2.0),
            Video("t3", "tablet case", "A", 3, 2.0),
        };

        var emerging = TrendDetector.Detect(TermAnalyzer.Index(videos, _extractor), Now, 7, 3, trackedChannelCount: 1);

        Assert.Equal(Confidence.Low, Assert.Single(emerging, e => e.Term == "tablet").Confidence);
    }
}
