using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.UnitTests.Analytics;

public sealed class ViewVelocityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static Observation At(double hours, long? views) => new(T0.AddHours(hours), views);

    [Fact]
    public void Uses_real_elapsed_time()
    {
        var result = ViewVelocity.Calculate([At(0, 1_000), At(36, 4_000)], null);

        Assert.Equal(2_000, result.Metric.Value); // 3,000 views over 1.5 days
        Assert.Equal(36, result.ElapsedHours);
        Assert.Equal(3_000, result.ViewsGained);
        Assert.Equal(Confidence.High, result.Metric.Confidence);
        Assert.Equal(MetricSource.Calculated, result.Metric.Source);
        Assert.Equal(2, result.Metric.SampleSize);
    }

    [Fact]
    public void Order_and_duplicate_timestamps_do_not_matter()
    {
        var result = ViewVelocity.Calculate([At(24, 2_000), At(0, 1_000), At(24, 2_000)], null);

        Assert.Equal(1_000, result.Metric.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Fewer_than_two_observations_is_unavailable(int count)
    {
        var result = ViewVelocity.Calculate(Enumerable.Range(0, count).Select(i => At(i, 100)), null);

        Assert.Null(result.Metric.Value);
        Assert.Equal(Confidence.Unavailable, result.Metric.Confidence);
        Assert.Equal(MetricSource.Unavailable, result.Metric.Source);
    }

    [Fact]
    public void Hidden_counts_are_ignored()
    {
        var result = ViewVelocity.Calculate([At(0, null), At(10, null), At(20, 500)], null);

        Assert.Null(result.Metric.Value);
    }

    [Fact]
    public void Window_picks_newest_baseline_at_least_that_old()
    {
        var result = ViewVelocity.Calculate([At(0, 0), At(30, 3_000), At(40, 4_000), At(54, 6_000)], TimeSpan.FromHours(24));

        // Latest at 54 h, cutoff 30 h, baseline = 30 h.
        Assert.Equal(T0.AddHours(30), result.FromUtc);
        Assert.Equal(3_000, result.Metric.Value); // 3,000 views in 1 day
        Assert.Equal(3, result.Metric.SampleSize);
    }

    [Fact]
    public void Short_history_uses_oldest_and_says_so()
    {
        var result = ViewVelocity.Calculate([At(0, 0), At(12, 600)], TimeSpan.FromHours(24));

        Assert.Equal(1_200, result.Metric.Value);
        Assert.Contains(result.Metric.Notes, n => n.Contains("shorter than", StringComparison.Ordinal));
    }

    [Fact]
    public void Decreasing_count_is_low_confidence_with_note()
    {
        var result = ViewVelocity.Calculate([At(0, 1_000), At(24, 900)], null);

        Assert.Equal(-100, result.Metric.Value);
        Assert.Equal(Confidence.Low, result.Metric.Confidence);
        Assert.Contains(result.Metric.Notes, n => n.Contains("went down", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0.5, Confidence.Low)]
    [InlineData(3, Confidence.Medium)]
    [InlineData(6, Confidence.High)]
    public void Confidence_grows_with_elapsed_time(double hours, Confidence expected) =>
        Assert.Equal(expected, ViewVelocity.Calculate([At(0, 0), At(hours, 100)], null).Metric.Confidence);
}
