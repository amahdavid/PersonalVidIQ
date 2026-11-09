using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.Research;
using PersonalVidIQ.Application.Services;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Infrastructure.YouTube;
using PersonalVidIQ.UnitTests.Services;

namespace PersonalVidIQ.IntegrationTests.Persistence;

/// <summary>Milestone 6 research over real stored data (tracked through the real tracking service) and a fake YouTube.</summary>
public sealed class ResearchServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly FakeYouTube _youTube = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly IOptions<DataCollectionOptions> _collection = Options.Create(new DataCollectionOptions());
    private readonly IOptions<ResearchOptions> _research = Options.Create(new ResearchOptions());

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private SnapshotStore Snapshots => new(db.Factory, _collection);

    private ChannelVideoLoader Loader => new(_youTube, Snapshots, _collection, _time);

    private ResearchDataLoader DataLoader => new(new ResearchDataStore(db.Factory), _collection, _time);

    private TrackingService Tracking => new(_youTube, new ChannelStore(db.Factory, _time), new VideoStore(db.Factory, _time), Snapshots, Loader, _collection);

    private KeywordResearchService Keywords => new(
        _youTube, new InMemoryQuotaLedger(Options.Create(new YouTubeOptions()), _time), DataLoader, Loader, _research, _collection, _time);

    private TrendResearchService Trends => new(DataLoader, new ChannelStore(db.Factory, _time), Loader, _research, _time);

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();

        // Three channels in a cooking niche. Each has 6 ordinary videos (~1,000 views/day) plus some keyword videos.
        AddChannel("A", "@chefa");
        AddChannel("B", "@chefb");
        AddChannel("M", "@mine");
        for (var i = 0; i < 6; i++)
        {
            AddVideo($"A-plain-{i}", "A", $"Weeknight dinner idea number {i}", 4 + i, 1_000);
            AddVideo($"B-plain-{i}", "B", $"Simple lunch box {i}", 4 + i, 1_000);
            AddVideo($"M-plain-{i}", "M", $"My kitchen vlog {i}", 4 + i, 1_000);
        }

        // "air fryer" is new this week, used by A and B, and doing 3-4x their usual.
        AddVideo("A-af-1", "A", "Air fryer chicken wings", 2, 4_000);
        AddVideo("A-af-2", "A", "Air fryer salmon", 3, 3_000);
        AddVideo("B-af-1", "B", "Crispy air fryer potatoes", 2, 3_500);
        _youTube.Videos["upcoming1"] = TestData.Video("upcoming1") with { ChannelId = Id("A"), Title = "Air fryer live", LiveBroadcastContent = "upcoming" };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string Id(string key) => $"UC{key}{new string('x', 21)}";

    private void AddChannel(string key, string handle)
    {
        _youTube.Channels[Id(key)] = TestData.Channel(Id(key)) with { CustomUrl = handle, Title = $"Chef {key}", UploadsPlaylistId = $"UU{key}" };
        _youTube.Playlists[$"UU{key}"] = [];
    }

    private void AddVideo(string id, string channelKey, string title, double ageDays, long viewsPerDay)
    {
        _youTube.Videos[id] = TestData.Video(id, channelId: Id(channelKey)) with
        {
            Title = title,
            Tags = [],
            PublishedAtUtc = TestData.Now.AddDays(-ageDays),
            ViewCount = (long)(viewsPerDay * ageDays),
            ChannelTitle = $"Chef {channelKey}",
        };
        _youTube.Playlists[$"UU{channelKey}"].Add(id);
    }

    private async Task TrackAll()
    {
        await Tracking.TrackChannelAsync("@chefa", Ct);
        await Tracking.TrackChannelAsync("@chefb", Ct);
        await Tracking.TrackChannelAsync("@mine", Ct);
    }

    [Fact]
    public async Task Keyword_ideas_rank_keywords_by_channel_relative_performance()
    {
        await TrackAll();

        var report = await Keywords.GetKeywordIdeasAsync(null, null, Ct);

        Assert.Equal(3, report.Data.Channels);
        var top = report.BestPerformingKeywords[0];
        Assert.Equal("air fryer", top.Term);
        Assert.Equal(3, top.Videos);
        Assert.Equal(2, top.Channels);
        Assert.True(top.MedianChannelRelativeRatio > 2.5);
        Assert.Contains(report.Notes, n => n.Contains("search volume", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Keyword_ideas_with_seed_list_what_goes_with_it()
    {
        await TrackAll();

        var report = await Keywords.GetKeywordIdeasAsync("air fryer", null, Ct);

        Assert.Equal("air fryer", report.Seed);
        Assert.Equal(3, report.Data.Videos);
        Assert.DoesNotContain(report.MostUsedKeywords, k => k.Term is "air fryer" or "air" or "fryer");
    }

    [Fact]
    public async Task Trends_find_the_new_keyword_used_across_channels()
    {
        await TrackAll();

        var report = await Trends.DiscoverTrendsAsync(7, null, Ct);

        var airFryer = Assert.Single(report.Emerging, e => e.Term == "air fryer");
        Assert.Equal(2, airFryer.RecentChannels);
        Assert.Equal(0, airFryer.PriorVideos);
        Assert.Contains("outperforming", airFryer.Signal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recent_uploads_list_newest_first()
    {
        await TrackAll();

        var report = await Trends.GetRecentUploadsAsync(60, Ct);

        Assert.Equal(["A-af-1", "B-af-1"], report.Uploads.Select(u => u.Video.VideoId).Order());
    }

    [Fact]
    public async Task Idea_evidence_finds_gaps_against_my_channel()
    {
        await TrackAll();

        var report = await Trends.GetVideoIdeaEvidenceAsync("@mine", 7, Ct);

        Assert.Equal("Chef M", report.MyChannel);
        Assert.DoesNotContain(report.RecentOutliers, v => v.ChannelTitle == "Chef M");
        Assert.Contains(report.RecentOutliers, v => v.VideoId == "A-af-1");
        Assert.Contains(report.KeywordGaps, g => g.Term == "air fryer");
        Assert.NotEmpty(report.HowToUse);
    }

    [Fact]
    public async Task Nothing_tracked_explains_what_to_do()
    {
        var report = await Keywords.GetKeywordIdeasAsync(null, null, Ct);

        Assert.Equal(0, report.Data.Channels);
        Assert.Contains(report.Notes, n => n.Contains("track_channel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Research_keyword_reports_competition_without_search_volume()
    {
        AddChannel("S", "@small");
        _youTube.Channels[Id("S")] = _youTube.Channels[Id("S")] with { SubscriberCount = 2_000 };
        AddVideo("S-af-1", "S", "Air fryer chicken wings", 10, 500);
        _youTube.SearchResults.AddRange(["A-af-1", "B-af-1", "S-af-1", "A-plain-1"]);

        var report = await Keywords.ResearchKeywordAsync("air fryer", null, null, Ct);

        Assert.Equal(4, report.ResultsAnalysed);
        var metrics = report.Competition.ToDictionary(m => m.Name);
        Assert.Equal(75, metrics["share_with_keyword_in_title"].Value);
        Assert.Equal(3, metrics["distinct_channels"].Value);
        Assert.Equal(25, metrics["share_from_channels_under_10000_subscribers"].Value);
        Assert.Contains(report.RelatedTerms, r => r.Term == "chicken wings" && r.Results == 2);
        Assert.Contains(report.Notes, n => n.Contains("search volume", StringComparison.Ordinal));
    }
}
