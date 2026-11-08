using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.Services;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Infrastructure.YouTube;
using PersonalVidIQ.UnitTests.Services;

namespace PersonalVidIQ.IntegrationTests.Persistence;

/// <summary>The collector against real PostgreSQL stores and a fake YouTube.</summary>
public sealed class CollectionServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private const string ChannelId = "UCaaaaaaaaaaaaaaaaaaaaaa";
    private const string Playlist = "UUaaaaaaaaaaaaaaaaaaaaaa";

    private readonly FakeYouTube _youTube = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly DataCollectionOptions _collection = new() { InitialUploadsToTrack = 50 };
    private readonly YouTubeOptions _youTubeOptions = new();
    private InMemoryQuotaLedger? _ledger;

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private IOptions<DataCollectionOptions> Options => Microsoft.Extensions.Options.Options.Create(_collection);

    private InMemoryQuotaLedger Ledger => _ledger ??= new InMemoryQuotaLedger(Microsoft.Extensions.Options.Options.Create(_youTubeOptions), _time);

    private SnapshotStore Snapshots => new(db.Factory, Options);

    private TrackingService Tracking => new(_youTube, new ChannelStore(db.Factory, _time), new VideoStore(db.Factory, _time), Snapshots,
        new ChannelVideoLoader(_youTube, Snapshots, Options, _time), Options);

    private CollectionService Collector => new(
        _youTube,
        new CollectionStateStore(db.Factory),
        new CollectionRunLog(db.Factory, _time),
        new ChannelStore(db.Factory, _time),
        new VideoStore(db.Factory, _time),
        Snapshots,
        new RetentionService(db.Factory, Options, _time, NullLogger<RetentionService>.Instance),
        Ledger,
        Options,
        _time,
        NullLogger<CollectionService>.Instance);

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        _youTube.Channels[ChannelId] = TestData.Channel(ChannelId) with { CustomUrl = "@footballfacts" };
        // One upload 10 h old (new tier) and one 5 days old (recent tier).
        AddVideo("newupload01", TimeSpan.FromHours(10));
        AddVideo("olderupload", TimeSpan.FromDays(5));
        _youTube.Playlists[Playlist] = ["newupload01", "olderupload"];
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void AddVideo(string id, TimeSpan age, long views = 1_000) =>
        _youTube.Videos[id] = TestData.Video(id) with { PublishedAtUtc = _time.GetUtcNow() - age, ViewCount = views };

    private void Advance(TimeSpan by)
    {
        _time.Advance(by);
        // Fresh reads are stamped with the current time.
        foreach (var id in _youTube.Videos.Keys.ToList())
        {
            _youTube.Videos[id] = _youTube.Videos[id] with { RetrievedAtUtc = _time.GetUtcNow(), ViewCount = _youTube.Videos[id].ViewCount + 500 };
        }

        _youTube.Channels[ChannelId] = _youTube.Channels[ChannelId] with { RetrievedAtUtc = _time.GetUtcNow() };
    }

    [Fact]
    public async Task Nothing_is_due_right_after_tracking()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);

        var report = await Collector.CollectAsync(new CollectRequest("test"), Ct);

        Assert.Equal("Succeeded", report.Status);
        Assert.Equal(0, report.SnapshotsSaved);
        Assert.Equal(2, report.VideosNotDue);
        Assert.Contains(report.Notes, n => n.Contains("Nothing is due", StringComparison.Ordinal));
        Assert.NotNull(report.NextDueUtc);
    }

    [Fact]
    public async Task After_three_hours_new_videos_and_uploads_are_refreshed_and_new_uploads_tracked()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);
        Advance(TimeSpan.FromHours(3));
        AddVideo("brandnew001", TimeSpan.FromMinutes(30));
        _youTube.Playlists[Playlist].Insert(0, "brandnew001");
        var callsBefore = _youTube.Calls;

        var report = await Collector.CollectAsync(new CollectRequest("test"), Ct);

        Assert.Equal("Succeeded", report.Status);
        Assert.Equal(0, report.ChannelsSnapshotted);         // daily, not due
        Assert.Equal(1, report.UploadChecks);                // every 3 h
        Assert.Equal("brandnew001", Assert.Single(report.NewUploadsTracked).VideoId);
        Assert.Equal(1, report.VideosRefreshedByTier["new"]); // newupload01
        Assert.Equal(1, report.VideosNotDue);                // olderupload (daily)
        Assert.Equal(2, report.SnapshotsSaved);               // brandnew001 + newupload01
        Assert.Equal(3, _youTube.Calls - callsBefore);        // playlist + videos.list(new) + videos.list(due)

        var history = await Snapshots.GetVideoSnapshotsAsync("newupload01", null, Ct);
        Assert.Equal(2, history.Count);

        await using var ctx = await db.Factory.CreateDbContextAsync(Ct);
        var run = await ctx.CollectionRuns.SingleAsync(Ct);
        Assert.Equal("Succeeded", run.Status);
        Assert.Equal(1, run.NewVideosTracked);
    }

    [Fact]
    public async Task Deleted_videos_are_untracked_and_reported()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);
        Advance(TimeSpan.FromDays(1));
        _youTube.Videos.Remove("olderupload");
        _youTube.Playlists[Playlist].Remove("olderupload");

        var report = await Collector.CollectAsync(new CollectRequest("test"), Ct);

        Assert.Equal(["olderupload"], report.NoLongerAvailable);
        Assert.Equal(1, report.ChannelsSnapshotted);
        Assert.False((await new VideoStore(db.Factory, _time).FindAsync("olderupload", Ct))!.IsTracked);
    }

    [Fact]
    public async Task Dry_run_plans_without_fetching_or_saving()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);
        Advance(TimeSpan.FromDays(2));
        var callsBefore = _youTube.Calls;

        var report = await Collector.CollectAsync(new CollectRequest("test", DryRun: true), Ct);

        Assert.Equal("Planned", report.Status);
        Assert.Equal(callsBefore, _youTube.Calls);
        Assert.Equal(0, report.SnapshotsSaved);
        Assert.Equal(1 + 1 + 1 + 1, report.EstimatedUnits); // channels.list, playlist, new-uploads videos.list, due videos.list
    }

    [Fact]
    public async Task Force_refreshes_everything_but_respects_snapshot_spacing()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);
        Advance(TimeSpan.FromMinutes(20));

        var report = await Collector.CollectAsync(new CollectRequest("test", Force: true), Ct);

        Assert.Equal(1, report.ChannelsSnapshotted);
        Assert.Equal(2, report.VideosRefreshedByTier.Values.Sum());
        Assert.Equal(3, report.SnapshotsSaved);
    }

    [Fact]
    public async Task Quota_reserve_skips_video_work()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);
        Advance(TimeSpan.FromDays(2));
        _youTubeOptions.DailyQuotaUnits = 1_000; // everything left is the 1,000-unit reserve
        var callsBefore = _youTube.Calls;

        var report = await Collector.CollectAsync(new CollectRequest("test"), Ct);

        Assert.Equal(2, report.VideosSkippedForQuota);
        Assert.Equal(callsBefore, _youTube.Calls);
        Assert.Contains(report.Notes, n => n.Contains("Quota budget", StringComparison.Ordinal));
    }
}
