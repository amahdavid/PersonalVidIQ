using PersonalVidIQ.Application.Analytics;

namespace PersonalVidIQ.UnitTests.Analytics;

public sealed class LifetimeAndRatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lifetime_average_divides_by_age_in_days()
    {
        var metric = LifetimeAverage.ViewsPerDay(10_000, Now.AddDays(-4), Now);

        Assert.Equal(2_500, metric.Value);
        Assert.Equal(Confidence.Medium, metric.Confidence);
    }

    [Fact]
    public void Very_new_video_uses_one_hour_minimum_and_low_confidence()
    {
        var metric = LifetimeAverage.ViewsPerDay(500, Now.AddMinutes(-10), Now);

        Assert.Equal(12_000, metric.Value); // 500 views per hour
        Assert.Equal(Confidence.Low, metric.Confidence);
        Assert.Contains(metric.Notes, n => n.Contains("less than an hour", StringComparison.Ordinal));
    }

    [Fact]
    public void Lifetime_average_unavailable_without_views_or_date()
    {
        Assert.Null(LifetimeAverage.ViewsPerDay(null, Now.AddDays(-1), Now).Value);
        Assert.Null(LifetimeAverage.ViewsPerDay(100, null, Now).Value);
    }

    [Fact]
    public void Like_and_comment_rates_are_percentages()
    {
        Assert.Equal(4.8, EngagementRates.LikeRate(12_000, 250_000).Value);
        Assert.Equal(0.136, EngagementRates.CommentRate(340, 250_000).Value);
    }

    [Fact]
    public void Rates_flag_small_samples_and_hidden_counts()
    {
        Assert.Equal(Confidence.Low, EngagementRates.LikeRate(10, 200).Confidence);
        Assert.Null(EngagementRates.LikeRate(null, 1_000).Value);
        Assert.Null(EngagementRates.CommentRate(5, 0).Value);
    }

    [Theory]
    [InlineData(new double[] { 3, 1, 2 }, 2)]
    [InlineData(new double[] { 4, 1, 3, 2 }, 2.5)]
    public void Median(double[] values, double expected) => Assert.Equal(expected, Statistics.Median(values));

    [Fact]
    public void Median_of_nothing_is_null() => Assert.Null(Statistics.Median([]));
}
