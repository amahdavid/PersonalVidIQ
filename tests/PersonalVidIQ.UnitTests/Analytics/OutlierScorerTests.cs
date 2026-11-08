using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.UnitTests.Analytics;

public sealed class OutlierScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly OutlierSettings Settings = new();

    private static VideoAnalyticsInput Video(
        string id, double ageDays, long views, int seconds = 45, IReadOnlyList<Observation>? history = null, string live = "none") =>
        new(id, $"Video {id}", Now.AddDays(-ageDays), TimeSpan.FromSeconds(seconds), live, views, null, null, Now, history ?? []);

    /// <summary>Ten Shorts aged 5-14 days, each at 1,000 views/day.</summary>
    private static List<VideoAnalyticsInput> Baseline(int count = 10, double startAge = 5, int seconds = 45) =>
        Enumerable.Range(0, count).Select(i => Video($"base{i:D7}", startAge + i, (long)((startAge + i) * 1_000), seconds)).ToList();

    [Fact]
    public void Lifetime_method_compares_against_same_age_same_format_median()
    {
        var target = Video("target00001", 8, 40_000); // 5,000/day vs 1,000/day
        var videos = Baseline().Append(target).ToList();

        var result = OutlierScorer.Score(target, videos, Settings, Now);

        Assert.Equal(OutlierMethod.LifetimeAverage, result.Method);
        Assert.Equal(VideoFormat.Short, result.Format);
        Assert.Equal(5_000, result.VideoValue);
        Assert.Equal(1_000, result.CohortMedian);
        Assert.Equal(5, result.Ratio);
        Assert.Equal(OutlierBand.WellAbove, result.Band);
        Assert.Equal(10, result.CohortSize); // ages 5-14 all within 4-16 days
        Assert.False(result.CohortWidened);
        Assert.Equal(Confidence.High, result.Confidence);
    }

    [Fact]
    public void Long_form_videos_are_not_in_a_shorts_cohort()
    {
        var target = Video("target00001", 8, 8_000);
        var videos = Baseline(10, 5, seconds: 600).Append(target).ToList();

        var result = OutlierScorer.Score(target, videos, Settings, Now);

        Assert.Equal(OutlierBand.Unavailable, result.Band);
        Assert.Equal(Confidence.Unavailable, result.Confidence);
        Assert.Contains("Shorts", result.Notes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Old_videos_are_kept_out_of_a_new_videos_cohort()
    {
        // Target is 2 days old; baseline videos are 100+ days old.
        var target = Video("target00001", 2, 4_000);
        var videos = Baseline(10, 100).Append(target).ToList();

        var result = OutlierScorer.Score(target, videos, Settings, Now);

        Assert.Equal(OutlierBand.Unavailable, result.Band);
    }

    [Fact]
    public void Cohort_is_widened_when_too_few_and_confidence_drops()
    {
        // Ages 13-16 are outside 0.5x-2x of 6 days (3-12) but inside 0.25x-4x (1.5-24).
        var target = Video("target00001", 6, 6_000);
        var videos = Baseline(5, 13).Append(target).ToList();

        var result = OutlierScorer.Score(target, videos, Settings, Now);

        Assert.True(result.CohortWidened);
        Assert.Equal(5, result.CohortSize);
        Assert.Equal(Confidence.Medium, result.Confidence);
        Assert.Equal(OutlierBand.Typical, result.Band);
    }

    [Fact]
    public void Snapshot_velocity_is_used_when_history_exists()
    {
        // Baseline lifetime 1,000/day, but over the last 2 days they've each gained only 100/day.
        var baseline = Enumerable.Range(0, 6).Select(i =>
        {
            var age = 6 + i;
            var views = age * 1_000L;
            return Video($"base{i:D7}", age, views, history: [new Observation(Now.AddDays(-2), views - 200)]);
        }).ToList();

        // Target lifetime 1,000/day too, but gained 2,000/day recently.
        var target = Video("target00001", 8, 8_000, history: [new Observation(Now.AddDays(-2), 4_000)]);
        var result = OutlierScorer.Score(target, baseline.Append(target).ToList(), Settings, Now);

        Assert.Equal(OutlierMethod.SnapshotVelocity, result.Method);
        Assert.Equal(2_000, result.VideoValue);
        Assert.Equal(100, result.CohortMedian);
        Assert.Equal(20, result.Ratio);
    }

    [Fact]
    public void Falls_back_to_lifetime_when_cohort_lacks_history()
    {
        var target = Video("target00001", 8, 8_000, history: [new Observation(Now.AddDays(-2), 4_000)]);
        var result = OutlierScorer.Score(target, Baseline().Append(target).ToList(), Settings, Now);

        Assert.Equal(OutlierMethod.LifetimeAverage, result.Method);
    }

    [Fact]
    public void Under_a_day_old_lifetime_score_is_low_confidence()
    {
        // Baseline: Shorts aged 0.5-2 days. Effective age floor of 1 day keeps the band sensible.
        var baseline = Enumerable.Range(0, 6).Select(i => Video($"base{i:D7}", 0.6 + (i * 0.2), 1_000)).ToList();
        var target = Video("target00001", 0.5, 3_000);

        var result = OutlierScorer.Score(target, baseline.Append(target).ToList(), Settings, Now);

        Assert.Equal(Confidence.Low, result.Confidence);
        Assert.NotNull(result.Ratio);
    }

    [Fact]
    public void Zero_median_is_unavailable_not_infinite()
    {
        var baseline = Enumerable.Range(0, 6).Select(i => Video($"base{i:D7}", 6 + i, 0)).ToList();
        var target = Video("target00001", 8, 1_000);

        var result = OutlierScorer.Score(target, baseline.Append(target).ToList(), Settings, Now);

        Assert.Null(result.Ratio);
        Assert.Equal(Confidence.Unavailable, result.Confidence);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("upcoming")]
    public void Live_and_upcoming_are_not_scored(string live)
    {
        var target = Video("target00001", 8, 8_000, live: live);

        Assert.Equal(OutlierBand.Unavailable, OutlierScorer.Score(target, Baseline(), Settings, Now).Band);
    }

    [Fact]
    public void Unknown_duration_is_not_scored()
    {
        var target = Video("target00001", 8, 8_000, seconds: 0);

        Assert.Equal(VideoFormat.Unknown, OutlierScorer.Score(target, Baseline(), Settings, Now).Format);
    }

    [Theory]
    [InlineData(3.0, OutlierBand.WellAbove)]
    [InlineData(1.5, OutlierBand.Above)]
    [InlineData(1.0, OutlierBand.Typical)]
    [InlineData(0.6, OutlierBand.Below)]
    public void Bands(double ratio, OutlierBand expected) => Assert.Equal(expected, OutlierScorer.BandFor(ratio));

    [Fact]
    public void ScoreAll_scores_each_video_against_the_others()
    {
        var videos = Baseline().Append(Video("target00001", 8, 40_000)).ToList();

        var results = OutlierScorer.ScoreAll(videos, Settings, Now);

        Assert.Equal(11, results.Count);
        Assert.Equal("target00001", results.MaxBy(r => r.Ratio ?? 0)!.VideoId);
    }
}
