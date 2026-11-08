using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Domain.Enums;
using PersonalVidIQ.Infrastructure.Persistence;

namespace PersonalVidIQ.IntegrationTests.Persistence;

public sealed class StoreTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private ChannelStore Channels => new(db.Factory, _time);

    private VideoStore Videos => new(db.Factory, _time);

    private SnapshotStore Snapshots => new(db.Factory, Options.Create(new DataCollectionOptions()));

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Channel_upsert_inserts_then_updates_and_keeps_tracking()
    {
        await Channels.UpsertAsync(TestData.Channel(), Ct);
        Assert.True(await Channels.SetTrackedAsync("UCaaaaaaaaaaaaaaaaaaaaaa", true, Ct));

        _time.Advance(TimeSpan.FromHours(1));
        var updated = await Channels.UpsertAsync(TestData.Channel(at: _time.GetUtcNow()) with { Title = "Renamed" }, Ct);

        var stored = await Channels.FindAsync("UCaaaaaaaaaaaaaaaaaaaaaa", Ct);
        Assert.NotNull(stored);
        Assert.Equal("Renamed", stored.Title);
        Assert.True(stored.IsTracked);
        Assert.Equal(TestData.Now, stored.TrackedAtUtc);
        Assert.Equal(_time.GetUtcNow(), stored.LastRefreshedAtUtc);
        Assert.Equal("UUaaaaaaaaaaaaaaaaaaaaaa", stored.UploadsPlaylistId);
        Assert.Equal(updated.Id, stored.Id);
        Assert.Single(await Channels.ListTrackedAsync(Ct));
    }

    [Fact]
    public async Task Unknown_channel_cannot_be_tracked()
    {
        Assert.False(await Channels.SetTrackedAsync("UCzzzzzzzzzzzzzzzzzzzzzz", true, Ct));
    }

    [Fact]
    public async Task Video_upsert_links_channel_and_tracks_with_first_source()
    {
        await Channels.UpsertAsync(TestData.Channel(), Ct);

        await Videos.UpsertAsync([TestData.Video("aaaaaaaaaaa"), TestData.Video("bbbbbbbbbbb", seconds: 600)], TrackingSource.Channel, Ct);
        await Videos.UpsertAsync([TestData.Video("aaaaaaaaaaa")], TrackingSource.Manual, Ct);

        var a = await Videos.FindAsync("aaaaaaaaaaa", Ct);
        Assert.NotNull(a);
        Assert.True(a.IsTracked);
        Assert.Equal(TrackingSource.Channel, a.TrackingSource); // first source wins
        Assert.Equal("UCaaaaaaaaaaaaaaaaaaaaaa", a.Channel!.YouTubeChannelId);
        Assert.Equal(58, a.DurationSeconds);
        Assert.Equal(["messi", "football"], a.Tags);

        var b = await Videos.FindAsync("bbbbbbbbbbb", Ct);
        Assert.Equal(600, b!.DurationSeconds);
        Assert.Equal(2, (await Videos.ListTrackedAsync(Ct)).Count);
        Assert.Equal(2, (await Videos.ListByChannelAsync("UCaaaaaaaaaaaaaaaaaaaaaa", Ct)).Count);
    }

    [Fact]
    public async Task Video_without_stored_channel_is_kept_unlinked()
    {
        await Videos.UpsertAsync([TestData.Video("ccccccccccc", channelId: "UCnotstoredxxxxxxxxxxxxx")], null, Ct);

        var video = await Videos.FindAsync("ccccccccccc", Ct);
        Assert.NotNull(video);
        Assert.Null(video.ChannelId);
        Assert.False(video.IsTracked);
    }

    [Fact]
    public async Task Untracking_a_channel_keeps_manually_tracked_videos()
    {
        await Channels.UpsertAsync(TestData.Channel(), Ct);
        await Videos.UpsertAsync([TestData.Video("aaaaaaaaaaa"), TestData.Video("bbbbbbbbbbb")], TrackingSource.Channel, Ct);
        await Videos.UpsertAsync([TestData.Video("mmmmmmmmmmm")], TrackingSource.Manual, Ct);

        var untracked = await Videos.UntrackChannelVideosAsync("UCaaaaaaaaaaaaaaaaaaaaaa", Ct);

        Assert.Equal(2, untracked);
        var remaining = await Videos.ListTrackedAsync(Ct);
        Assert.Equal("mmmmmmmmmmm", Assert.Single(remaining).YouTubeVideoId);
    }

    [Fact]
    public async Task Video_untrack_and_retrack_manually()
    {
        await Videos.UpsertAsync([TestData.Video("aaaaaaaaaaa")], TrackingSource.Channel, Ct);

        Assert.True(await Videos.SetTrackedAsync("aaaaaaaaaaa", false, Ct));
        var off = await Videos.FindAsync("aaaaaaaaaaa", Ct);
        Assert.False(off!.IsTracked);
        Assert.Null(off.TrackingSource);

        Assert.True(await Videos.SetTrackedAsync("aaaaaaaaaaa", true, Ct));
        Assert.Equal(TrackingSource.Manual, (await Videos.FindAsync("aaaaaaaaaaa", Ct))!.TrackingSource);
    }

    [Fact]
    public async Task Video_snapshots_are_idempotent_within_spacing()
    {
        await Videos.UpsertAsync([TestData.Video("aaaaaaaaaaa")], TrackingSource.Manual, Ct);

        Assert.Equal(1, await Snapshots.AddVideoSnapshotsAsync([TestData.Video("aaaaaaaaaaa", views: 1000)], Ct));
        Assert.Equal(0, await Snapshots.AddVideoSnapshotsAsync([TestData.Video("aaaaaaaaaaa", views: 1000)], Ct)); // same moment
        Assert.Equal(0, await Snapshots.AddVideoSnapshotsAsync([TestData.Video("aaaaaaaaaaa", at: TestData.Now.AddMinutes(10), views: 1100)], Ct));
        Assert.Equal(1, await Snapshots.AddVideoSnapshotsAsync([TestData.Video("aaaaaaaaaaa", at: TestData.Now.AddMinutes(20), views: 1200)], Ct));
        Assert.Equal(0, await Snapshots.AddVideoSnapshotsAsync([TestData.Video("zzzzzzzzzzz")], Ct)); // not stored

        var history = await Snapshots.GetVideoSnapshotsAsync("aaaaaaaaaaa", null, Ct);
        Assert.Equal([1000L, 1200L], history.Select(s => s.ViewCount!.Value));
        Assert.All(history, s => Assert.Equal(MetricSource.YouTubeApi, s.DataSource));

        var recent = await Snapshots.GetVideoSnapshotsAsync("aaaaaaaaaaa", TestData.Now.AddMinutes(1), Ct);
        Assert.Equal(1200, Assert.Single(recent).ViewCount);
    }

    [Fact]
    public async Task Channel_snapshots_store_hidden_subscriber_counts_as_null()
    {
        await Channels.UpsertAsync(TestData.Channel(), Ct);

        Assert.Equal(1, await Snapshots.AddChannelSnapshotsAsync([TestData.Channel(hidden: true)], Ct));

        var snapshot = Assert.Single(await Snapshots.GetChannelSnapshotsAsync("UCaaaaaaaaaaaaaaaaaaaaaa", null, Ct));
        Assert.True(snapshot.SubscriberCountHidden);
        Assert.Null(snapshot.SubscriberCount);
        Assert.Equal(123_456_789, snapshot.ViewCount);
    }
}
