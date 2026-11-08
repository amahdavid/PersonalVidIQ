using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Domain.Enums;
using PersonalVidIQ.Infrastructure.Persistence;

namespace PersonalVidIQ.IntegrationTests.Persistence;

public sealed class RetentionTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Purge_enforces_30_day_rules()
    {
        var options = Options.Create(new DataCollectionOptions());
        var channels = new ChannelStore(db.Factory, _time);
        var videos = new VideoStore(db.Factory, _time);
        var snapshots = new SnapshotStore(db.Factory, options);
        var start = TestData.Now;

        // Tracked channel + tracked video, refreshed on day 0 and day 35.
        await channels.UpsertAsync(TestData.Channel(at: start), Ct);
        await channels.SetTrackedAsync("UCaaaaaaaaaaaaaaaaaaaaaa", true, Ct);
        await videos.UpsertAsync([TestData.Video("trackedvid1", at: start)], TrackingSource.Manual, Ct);
        await snapshots.AddVideoSnapshotsAsync([TestData.Video("trackedvid1", at: start)], Ct);
        await snapshots.AddChannelSnapshotsAsync([TestData.Channel(at: start)], Ct);

        // Untracked video and an untracked channel with no videos, never refreshed again.
        await videos.UpsertAsync([TestData.Video("untracked01", channelId: "UCaaaaaaaaaaaaaaaaaaaaaa", at: start)], null, Ct);
        await channels.UpsertAsync(TestData.Channel("UCbbbbbbbbbbbbbbbbbbbbbb", at: start), Ct);

        // Search cache that has expired, and quota rows from 10 days ago.
        var cache = new PostgresSearchCache(db.Factory, _time);
        await cache.SetAsync("old", new SearchResult(new SearchRequest("q"), [], null, false, start), TimeSpan.FromHours(24), Ct);
        var ledger = new PostgresQuotaLedger(db.Factory, Options.Create(new YouTubeOptions()), _time);
        await ledger.RecordAsync("videos.list", 1, Ct);

        // 35 days later the tracked items are refreshed (new snapshot), the rest are not.
        _time.Advance(TimeSpan.FromDays(35));
        var later = _time.GetUtcNow();
        await channels.UpsertAsync(TestData.Channel(at: later), Ct);
        await videos.UpsertAsync([TestData.Video("trackedvid1", at: later)], null, Ct);
        await snapshots.AddVideoSnapshotsAsync([TestData.Video("trackedvid1", at: later)], Ct);

        var result = await new RetentionService(db.Factory, options, _time, NullLogger<RetentionService>.Instance).PurgeAsync(Ct);

        Assert.Equal(1, result.VideoSnapshotsDeleted);
        Assert.Equal(1, result.ChannelSnapshotsDeleted);
        Assert.Equal(1, result.StaleVideosDeleted);
        Assert.Equal(1, result.StaleChannelsDeleted);
        Assert.Equal(1, result.SearchCacheEntriesDeleted);
        Assert.Equal(1, result.QuotaRowsDeleted);

        await using var ctx = await db.Factory.CreateDbContextAsync(Ct);
        Assert.Equal(["trackedvid1"], await ctx.Videos.Select(v => v.YouTubeVideoId).ToListAsync(Ct));
        Assert.Equal(["UCaaaaaaaaaaaaaaaaaaaaaa"], await ctx.Channels.Select(c => c.YouTubeChannelId).ToListAsync(Ct));
        Assert.Equal(later, (await ctx.VideoSnapshots.SingleAsync(Ct)).CapturedAtUtc);

        // Running again deletes nothing.
        Assert.Equal(0, (await new RetentionService(db.Factory, options, _time, NullLogger<RetentionService>.Instance).PurgeAsync(Ct)).Total);
    }

    [Fact]
    public async Task Shorter_retention_window_is_respected()
    {
        var options = Options.Create(new DataCollectionOptions { SnapshotRetentionDays = 7 });
        await new VideoStore(db.Factory, _time).UpsertAsync([TestData.Video("aaaaaaaaaaa")], TrackingSource.Manual, Ct);
        var snapshots = new SnapshotStore(db.Factory, options);
        await snapshots.AddVideoSnapshotsAsync([TestData.Video("aaaaaaaaaaa", at: TestData.Now.AddDays(-8))], Ct);
        await snapshots.AddVideoSnapshotsAsync([TestData.Video("aaaaaaaaaaa", at: TestData.Now.AddDays(-6))], Ct);

        var result = await new RetentionService(db.Factory, options, _time, NullLogger<RetentionService>.Instance).PurgeAsync(Ct);

        Assert.Equal(1, result.VideoSnapshotsDeleted);
    }
}
