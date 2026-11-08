using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.UnitTests.Collection;

public sealed class DuePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly DuePolicy _policy = new(new DataCollectionOptions());

    [Theory]
    [InlineData(1, VideoTier.New)]
    [InlineData(47, VideoTier.New)]
    [InlineData(49, VideoTier.Recent)]
    [InlineData(24 * 29, VideoTier.Recent)]
    [InlineData(24 * 31, VideoTier.Old)]
    public void Tier_by_age(int ageHours, VideoTier expected) =>
        Assert.Equal(expected, _policy.TierFor(Now.AddHours(-ageHours), Now));

    [Fact]
    public void Intervals_follow_the_tiers()
    {
        Assert.Equal(TimeSpan.FromHours(3), _policy.IntervalFor(VideoTier.New));
        Assert.Equal(TimeSpan.FromHours(24), _policy.IntervalFor(VideoTier.Recent));
        Assert.Equal(TimeSpan.FromDays(7), _policy.IntervalFor(VideoTier.Old));
    }

    [Theory]
    [InlineData(null, true)]       // never captured
    [InlineData(2.0, false)]
    [InlineData(2.7, true)]        // 90% of 3 h
    [InlineData(10.0, true)]       // long gap after sleep
    public void Due_uses_real_elapsed_time_with_tolerance(double? hoursAgo, bool expected) =>
        Assert.Equal(expected, _policy.IsDue(hoursAgo is { } h ? Now.AddHours(-h) : null, TimeSpan.FromHours(3), Now));

    [Fact]
    public void Planner_orders_new_videos_first_and_reports_next_due()
    {
        var videos = new List<TrackedVideoState>
        {
            new("old", null, Now.AddDays(-60), Now.AddDays(-8)),         // weekly, due
            new("recent", null, Now.AddDays(-5), Now.AddHours(-30)),     // daily, due
            new("fresh", null, Now.AddHours(-10), Now.AddHours(-4)),     // 3-hourly, due
            new("notdue", null, Now.AddDays(-5), Now.AddHours(-2)),      // daily, due in ~19.6 h
        };
        var channels = new List<TrackedChannelState> { new("UCx", "X", "UUx", Now.AddHours(-1), Now.AddHours(-1)) };

        var plan = CollectionPlanner.Plan(channels, videos, _policy, Now, force: false);

        Assert.Equal(["fresh", "recent", "old"], plan.VideosToSnapshot.Select(v => v.Video.VideoId));
        Assert.Equal(1, plan.VideosNotDue);
        Assert.Empty(plan.ChannelsToSnapshot);
        Assert.Empty(plan.ChannelsToCheckForUploads);
        Assert.Equal(Now.AddHours(-1) + TimeSpan.FromHours(3 * 0.9), plan.NextDueUtc); // uploads check comes first
        Assert.Equal(1, plan.EstimatedUnits); // one videos.list batch
    }

    [Fact]
    public void Force_collects_everything()
    {
        var videos = new List<TrackedVideoState> { new("a", null, Now.AddDays(-5), Now.AddMinutes(-5)) };
        var channels = new List<TrackedChannelState> { new("UCx", "X", "UUx", Now.AddMinutes(-5), Now.AddMinutes(-5)) };

        var plan = CollectionPlanner.Plan(channels, videos, _policy, Now, force: true);

        Assert.Single(plan.VideosToSnapshot);
        Assert.Single(plan.ChannelsToSnapshot);
        Assert.Single(plan.ChannelsToCheckForUploads);
        Assert.Equal(1 + 1 + 1 + 1, plan.EstimatedUnits);
    }

    [Fact]
    public void Channels_without_uploads_playlist_are_not_checked()
    {
        var plan = CollectionPlanner.Plan([new("UCx", "X", null, null, null)], [], _policy, Now, force: false);

        Assert.Single(plan.ChannelsToSnapshot);
        Assert.Empty(plan.ChannelsToCheckForUploads);
    }
}
