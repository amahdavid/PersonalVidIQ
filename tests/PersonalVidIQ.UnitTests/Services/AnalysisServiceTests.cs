using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.Services;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.UnitTests.Services;

public sealed class AnalysisServiceTests
{
    private const string ChannelId = "UCaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeYouTube _youTube = new();
    private readonly FakeSnapshots _snapshots = new();
    private readonly FakeVideoStore _videos = new();
    private readonly FakeChannelStore _channels = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly IOptions<DataCollectionOptions> _options = Options.Create(new DataCollectionOptions());

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public AnalysisServiceTests()
    {
        _youTube.Channels[ChannelId] = new ChannelData(ChannelId, "Football Facts", "", "@footballfacts", null, null, "GB",
            "UUaaaaaaaaaaaaaaaaaaaaaa", 1_230_000, false, 50_000_000, 300, Now);

        // 12 Shorts aged 1-12 days at 1,000 views/day, one 3-day-old Short at 10,000/day, two long-form videos.
        var ids = new List<string>();
        for (var i = 1; i <= 12; i++)
        {
            ids.Add(AddVideo($"short{i:D6}", i, i * 1_000L, 40));
        }

        ids.Add(AddVideo("viralshort1", 3, 30_000, 50));
        ids.Add(AddVideo("longform001", 5, 50_000, 900));
        ids.Add(AddVideo("longform002", 40, 100_000, 900));
        _youTube.Playlists["UUaaaaaaaaaaaaaaaaaaaaaa"] = ids.OrderBy(id => _youTube.Videos[id].PublishedAtUtc).Reverse().ToList();
    }

    private string AddVideo(string id, double ageDays, long views, int seconds)
    {
        _youTube.Videos[id] = new VideoData(id, ChannelId, "Football Facts", $"Title {id}", "", Now.AddDays(-ageDays),
            TimeSpan.FromSeconds(seconds), null, [], "17", "none", views, views / 20, views / 500, Now);
        return id;
    }

    private VideoAnalysisService VideoService() =>
        new(_youTube, _videos, new ChannelVideoLoader(_youTube, _snapshots, _options, _time), _options, _time);

    private ChannelResearchService ChannelService() =>
        new(new ChannelVideoLoader(_youTube, _snapshots, _options, _time), _channels, _snapshots, _options, _time);

    [Fact]
    public async Task AnalyzeVideo_reports_api_stats_calculated_metrics_and_outlier()
    {
        var analysis = await VideoService().AnalyzeVideoAsync("https://www.youtube.com/shorts/viralshort1", Ct);

        Assert.Equal("viralshort1", analysis.Video.VideoId);
        Assert.Equal(VideoFormat.Short, analysis.Video.Format);
        Assert.Equal(30_000, analysis.YouTube.Views);

        var lifetime = analysis.Calculated.Single(m => m.Name == "lifetime_views_per_day");
        Assert.Equal(10_000, lifetime.Value);
        Assert.Null(analysis.Calculated.Single(m => m.Name == "views_per_day_last_24h").Value); // no history

        Assert.NotNull(analysis.Outlier);
        Assert.Equal(OutlierBand.WellAbove, analysis.Outlier.Band);
        Assert.Equal(1_000, analysis.Outlier.CohortMedian);
        Assert.Contains(analysis.Limitations, l => l.Contains("isn't tracked", StringComparison.Ordinal));
        Assert.Contains(analysis.Limitations, l => l.Contains("click-through", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeVideo_uses_stored_history_for_velocity_and_saves_the_fresh_read()
    {
        _videos.Tracked.Add("viralshort1");
        _snapshots.VideoHistory["viralshort1"] = [new Observation(Now.AddDays(-1), 20_000)];

        var analysis = await VideoService().AnalyzeVideoAsync("viralshort1", Ct);

        Assert.Equal(10_000, analysis.Calculated.Single(m => m.Name == "views_per_day_last_24h").Value);
        Assert.Equal(1, analysis.History.SnapshotCount);
        Assert.True(analysis.Video.IsTracked);
        Assert.Contains(_snapshots.VideoWrites, v => v.Id == "viralshort1");
    }

    [Fact]
    public async Task AnalyzeVideo_unknown_video_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => VideoService().AnalyzeVideoAsync("zzzzzzzzzzz", Ct));
        Assert.Equal(YouTubeErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public async Task CompareVideos_lists_found_and_missing()
    {
        var comparison = await VideoService().CompareVideosAsync(["viralshort1", "short000003", "zzzzzzzzzzz"], Ct);

        Assert.Equal(["viralshort1", "short000003"], comparison.Videos.Select(v => v.Video.VideoId));
        Assert.Equal(["zzzzzzzzzzz"], comparison.NotFound);
        Assert.All(comparison.Videos, v => Assert.Null(v.Outlier));
    }

    [Fact]
    public async Task CompareVideos_rejects_more_than_ten()
    {
        var ids = Enumerable.Range(0, 11).Select(i => $"vid{i:D8}").ToList();
        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => VideoService().CompareVideosAsync(ids, Ct));
        Assert.Equal(YouTubeErrorKind.InvalidInput, ex.Kind);
    }

    [Fact]
    public async Task FindOutliers_reports_only_window_and_threshold_sorted()
    {
        var report = await VideoService().FindOutliersAsync(new OutlierQuery("@footballfacts", PublishedWithinDays: 7, Format: VideoFormat.Short), Ct);

        Assert.Equal(15, report.VideosChecked);
        Assert.Equal(8, report.VideosInWindow); // Shorts aged 1-7 + viral
        var top = Assert.Single(report.Outliers);
        Assert.Equal("viralshort1", top.VideoId);
        Assert.Equal(ChannelId, report.Channel.ChannelId);
        Assert.Equal("https://www.youtube.com/@footballfacts", report.Channel.Url);
        Assert.Contains(_snapshots.ChannelWrites, c => c.Id == ChannelId);
    }

    [Fact]
    public async Task FindOutliers_rejects_bad_window()
    {
        await Assert.ThrowsAsync<YouTubeApiException>(() => VideoService().FindOutliersAsync(new OutlierQuery("@footballfacts", PublishedWithinDays: 0), Ct));
    }

    [Fact]
    public async Task CompareChannels_reports_activity_and_growth()
    {
        _channels.Tracked.Add(ChannelId);
        _snapshots.ChannelHistory.Add(new ChannelSnapshot { CapturedAtUtc = Now.AddDays(-5), ViewCount = 49_000_000, SubscriberCount = 1_200_000 });

        var comparison = await ChannelService().CompareChannelsAsync(["@footballfacts", "@missing"], 7, Ct);

        var entry = Assert.Single(comparison.Channels);
        Assert.Equal(["@missing"], comparison.NotFound);
        Assert.True(entry.IsTracked);
        Assert.Equal(1_230_000, entry.YouTube.Subscribers);

        Assert.Equal(9, entry.Activity.UploadsInPeriod); // Shorts aged 1-7, viral (3 d), long-form (5 d)
        Assert.Equal(8, entry.Activity.ShortsInPeriod);
        Assert.Equal(1, entry.Activity.LongFormInPeriod);
        Assert.Equal(9, entry.Activity.UploadsPerWeek.Value);

        Assert.Equal(200_000, entry.Growth.TotalViewsPerDay.Value); // 1,000,000 views over 5 days
        Assert.Equal(30_000, entry.Growth.SubscriberChange.Value);
        Assert.Equal(Confidence.Low, entry.Growth.SubscriberChange.Confidence);
    }

    [Fact]
    public async Task CompareChannels_without_history_has_unavailable_growth()
    {
        var comparison = await ChannelService().CompareChannelsAsync(["@footballfacts"], 30, Ct);

        var growth = Assert.Single(comparison.Channels).Growth;
        Assert.Null(growth.TotalViewsPerDay.Value);
        Assert.Null(growth.SubscriberChange.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task CompareChannels_period_is_limited_to_retention(int days)
    {
        await Assert.ThrowsAsync<YouTubeApiException>(() => ChannelService().CompareChannelsAsync(["@footballfacts"], days, Ct));
    }
}
