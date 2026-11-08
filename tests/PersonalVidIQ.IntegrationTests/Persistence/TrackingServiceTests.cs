using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.Services;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Infrastructure.YouTube;
using PersonalVidIQ.UnitTests.Services;

namespace PersonalVidIQ.IntegrationTests.Persistence;

/// <summary>Tracking and lookup services against real PostgreSQL stores and a fake YouTube.</summary>
public sealed class TrackingServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private const string ChannelId = "UCaaaaaaaaaaaaaaaaaaaaaa";

    private readonly FakeYouTube _youTube = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly IOptions<DataCollectionOptions> _options = Options.Create(new DataCollectionOptions { InitialUploadsToTrack = 50 });

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private ChannelStore Channels => new(db.Factory, _time);

    private VideoStore Videos => new(db.Factory, _time);

    private SnapshotStore Snapshots => new(db.Factory, _options);

    private ChannelVideoLoader Loader => new(_youTube, Snapshots, _options, _time);

    private TrackingService Tracking => new(_youTube, Channels, Videos, Snapshots, Loader, _options);

    private ResearchService Research => new(
        _youTube, new InMemoryQuotaLedger(Options.Create(new YouTubeOptions()), _time), Channels, Videos, Snapshots, Loader, _options);

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        _youTube.Channels[ChannelId] = TestData.Channel(ChannelId) with { CustomUrl = "@footballfacts" };
        _youTube.Channels["UCbbbbbbbbbbbbbbbbbbbbbb"] = TestData.Channel("UCbbbbbbbbbbbbbbbbbbbbbb") with { CustomUrl = "@other", Title = "Other" };
        var ids = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var id = $"upload{i:D5}";
            _youTube.Videos[id] = TestData.Video(id) with { PublishedAtUtc = TestData.Now.AddDays(-i - 1), ViewCount = 1_000 * (i + 1) };
            ids.Add(id);
        }

        _youTube.Videos["othervideo1"] = TestData.Video("othervideo1", channelId: "UCbbbbbbbbbbbbbbbbbbbbbb");
        _youTube.Playlists["UUaaaaaaaaaaaaaaaaaaaaaa"] = ids;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Track_channel_stores_channel_uploads_and_snapshots()
    {
        var result = await Tracking.TrackChannelAsync("@footballfacts", Ct);

        Assert.False(result.WasAlreadyTracked);
        Assert.Equal(6, result.RecentUploadsTracked);
        Assert.Equal(7, result.SnapshotsSaved); // 1 channel + 6 videos

        var list = await Tracking.ListTrackedAsync(Ct);
        var channel = Assert.Single(list.Channels);
        Assert.Equal(6, channel.VideosTracked);
        Assert.Equal(6, list.TotalTrackedVideos);
        Assert.Empty(list.ManuallyTrackedVideos);
        Assert.NotNull(channel.LastUploadsCheckAtUtc);

        Assert.True((await Tracking.TrackChannelAsync(ChannelId, Ct)).WasAlreadyTracked);
    }

    [Fact]
    public async Task Untrack_channel_by_handle_keeps_manual_videos()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);
        await Tracking.TrackVideoAsync("upload00002", Ct); // already channel-tracked: stays channel-sourced
        await Tracking.TrackVideoAsync("https://youtu.be/othervideo1", Ct);

        var result = await Tracking.UntrackChannelAsync("https://www.youtube.com/@FootballFacts", Ct);

        Assert.True(result.Found);
        Assert.Equal(6, result.VideosUntracked);
        var list = await Tracking.ListTrackedAsync(Ct);
        Assert.Empty(list.Channels);
        Assert.Equal("othervideo1", Assert.Single(list.ManuallyTrackedVideos).VideoId);
    }

    [Fact]
    public async Task Track_video_stores_its_channel_untracked()
    {
        var result = await Tracking.TrackVideoAsync("othervideo1", Ct);

        Assert.False(result.WasAlreadyTracked);
        Assert.Equal(1, result.SnapshotsSaved);
        var channel = await Channels.FindAsync("UCbbbbbbbbbbbbbbbbbbbbbb", Ct);
        Assert.NotNull(channel);
        Assert.False(channel.IsTracked);
        Assert.Equal("UCbbbbbbbbbbbbbbbbbbbbbb", (await Videos.FindAsync("othervideo1", Ct))!.Channel!.YouTubeChannelId);
    }

    [Fact]
    public async Task Untracking_something_not_tracked_says_so()
    {
        Assert.False((await Tracking.UntrackVideoAsync("othervideo1", Ct)).Found);
        Assert.False((await Tracking.UntrackChannelAsync("@nobody", Ct)).Found);
    }

    [Fact]
    public async Task Track_unknown_video_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => Tracking.TrackVideoAsync("zzzzzzzzzzz", Ct));
        Assert.Equal(YouTubeErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public async Task Channel_videos_popular_sorts_by_views_and_marks_tracked()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);

        var report = await Research.GetChannelVideosAsync("@footballfacts", 3, ChannelVideoOrder.Popular, null, Ct);

        Assert.Equal(["upload00005", "upload00004", "upload00003"], report.Videos.Select(v => v.Video.VideoId));
        Assert.All(report.Videos, v => Assert.True(v.Video.IsTracked));
        Assert.Equal(6, report.UploadsScanned);
        Assert.Contains(report.Notes, n => n.Contains("Popular", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_channel_reports_tracking_and_history()
    {
        await Tracking.TrackChannelAsync("@footballfacts", Ct);

        var report = await Research.GetChannelAsync(ChannelId, Ct);

        Assert.True(report.IsTracked);
        Assert.Equal(1, report.StoredSnapshots);
        Assert.Equal(1_230_000, report.YouTube.Subscribers);
    }

    [Fact]
    public async Task Get_video_returns_stats_and_rates()
    {
        var report = await Research.GetVideoAsync("upload00000", Ct);

        Assert.Equal(1_000, report.YouTube.Views);
        Assert.Equal(["lifetime_views_per_day", "like_rate", "comment_rate"], report.Calculated.Select(c => c.Name));
        Assert.False(report.Video.IsTracked);
    }
}
