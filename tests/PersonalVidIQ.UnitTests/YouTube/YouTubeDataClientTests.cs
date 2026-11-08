using System.Net;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Infrastructure.YouTube;

namespace PersonalVidIQ.UnitTests.YouTube;

public sealed class YouTubeDataClientTests
{
    private const string ApiKey = "TEST-KEY-123";

    private readonly FakeHttpHandler _http = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly YouTubeOptions _options = new() { ApiKey = ApiKey, MaxRetries = 2 };
    private InMemoryQuotaLedger? _ledger;

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private InMemoryQuotaLedger Ledger => _ledger ??= new InMemoryQuotaLedger(Options.Create(_options), _time);

    private YouTubeDataClient CreateClient() => new(
        new HttpClient(_http) { BaseAddress = new Uri(_options.BaseUrl) },
        Options.Create(_options),
        Ledger,
        new MemoryCache(new MemoryCacheOptions()),
        _time,
        NullLogger<YouTubeDataClient>.Instance);

    private static Dictionary<string, string?> Query(Uri uri)
    {
        var parsed = HttpUtility.ParseQueryString(uri.Query);
        return parsed.AllKeys.ToDictionary(k => k!, k => parsed[k]);
    }

    // ---- channels ----

    [Fact]
    public async Task GetChannels_maps_fields_and_hidden_subscriber_counts()
    {
        _http.RespondFixture("channels-two");

        var channels = await CreateClient().GetChannelsAsync(["UCaaaaaaaaaaaaaaaaaaaaaa", "UCbbbbbbbbbbbbbbbbbbbbbb"], Ct);

        Assert.Equal(2, channels.Count);
        var a = channels[0];
        Assert.Equal("Football Facts", a.Title);
        Assert.Equal("@footballfacts", a.CustomUrl);
        Assert.Equal("UUaaaaaaaaaaaaaaaaaaaaaa", a.UploadsPlaylistId);
        Assert.Equal(1_230_000, a.SubscriberCount);
        Assert.Equal(123_456_789, a.ViewCount);
        Assert.Equal(842, a.VideoCount);
        Assert.Equal("https://yt3.ggpht.com/a-high.jpg", a.ThumbnailUrl);
        Assert.Equal(_time.GetUtcNow(), a.RetrievedAtUtc);

        var b = channels[1];
        Assert.True(b.SubscriberCountHidden);
        Assert.Null(b.SubscriberCount);

        var query = Query(Assert.Single(_http.Requests));
        Assert.Equal("UCaaaaaaaaaaaaaaaaaaaaaa,UCbbbbbbbbbbbbbbbbbbbbbb", query["id"]);
        Assert.Equal("snippet,statistics,contentDetails", query["part"]);
        Assert.Equal(ApiKey, query["key"]);
        Assert.EndsWith("/channels", _http.Requests[0].AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetChannel_by_handle_uses_forHandle()
    {
        _http.RespondFixture("channels-two");

        var channel = await CreateClient().GetChannelAsync(new ChannelRef(ChannelRefKind.Handle, "@footballfacts"), Ct);

        Assert.Equal("UCaaaaaaaaaaaaaaaaaaaaaa", channel.Id);
        Assert.Equal("@footballfacts", Query(_http.Requests[0])["forHandle"]);
    }

    [Fact]
    public async Task GetChannel_with_no_items_throws_not_found()
    {
        _http.RespondFixture("channels-empty");

        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() =>
            CreateClient().GetChannelAsync(new ChannelRef(ChannelRefKind.Handle, "@nobody"), Ct));

        Assert.Equal(YouTubeErrorKind.NotFound, ex.Kind);
        Assert.Contains("@nobody", ex.Message, StringComparison.Ordinal);
    }

    // ---- videos ----

    [Fact]
    public async Task GetVideos_maps_fields_and_reports_missing_ids()
    {
        _http.RespondFixture("videos-two");

        var batch = await CreateClient().GetVideosAsync(["aaaaaaaaaaa", "bbbbbbbbbbb", "zzzzzzzzzzz", "aaaaaaaaaaa"], Ct);

        Assert.Equal(["zzzzzzzzzzz"], batch.MissingIds);
        var shortVideo = batch.Videos[0];
        Assert.Equal(TimeSpan.FromSeconds(58), shortVideo.Duration);
        Assert.Equal(250_000, shortVideo.ViewCount);
        Assert.Equal(12_000, shortVideo.LikeCount);
        Assert.Equal(["messi", "football"], shortVideo.Tags);
        Assert.Equal("Messi's craziest stat 🤯", shortVideo.Title);

        var longVideo = batch.Videos[1];
        Assert.Equal(new TimeSpan(0, 12, 3), longVideo.Duration);
        Assert.Null(longVideo.LikeCount); // hidden likes stay null, not zero
        Assert.Empty(longVideo.Tags);
        Assert.Equal("https://i.ytimg.com/vi/bbbbbbbbbbb/maxresdefault.jpg", longVideo.ThumbnailUrl);

        Assert.Equal("aaaaaaaaaaa,bbbbbbbbbbb,zzzzzzzzzzz", Query(Assert.Single(_http.Requests))["id"]);
    }

    [Fact]
    public async Task GetVideos_batches_50_ids_per_call()
    {
        var ids = Enumerable.Range(0, 120).Select(i => $"vid{i:D8}").ToList();
        _http.Respond(HttpStatusCode.OK, """{"items":[]}""")
             .Respond(HttpStatusCode.OK, """{"items":[]}""")
             .Respond(HttpStatusCode.OK, """{"items":[]}""");

        var batch = await CreateClient().GetVideosAsync(ids, Ct);

        Assert.Equal(3, _http.Requests.Count);
        Assert.Equal([50, 50, 20], _http.Requests.Select(r => Query(r)["id"]!.Split(',').Length));
        Assert.Equal(120, batch.MissingIds.Count);
        Assert.Equal(3, Ledger.GetUsage().UnitsUsed);
    }

    // ---- playlist ----

    [Fact]
    public async Task GetPlaylistVideos_follows_page_tokens_up_to_the_limit()
    {
        _http.RespondFixture("playlist-page1").RespondFixture("playlist-page2");

        var videos = await CreateClient().GetPlaylistVideosAsync("UUaaaaaaaaaaaaaaaaaaaaaa", 10, Ct);

        Assert.Equal(["v1111111111", "v2222222222", "v3333333333"], videos.Select(v => v.VideoId));
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 20, 0, 0, TimeSpan.Zero), videos[0].PublishedAtUtc);
        Assert.Equal(2, _http.Requests.Count);
        Assert.False(Query(_http.Requests[0]).ContainsKey("pageToken"));
        Assert.Equal("PAGE2", Query(_http.Requests[1])["pageToken"]);
    }

    [Fact]
    public async Task GetPlaylistVideos_stops_when_limit_reached()
    {
        _http.RespondFixture("playlist-page1");

        var videos = await CreateClient().GetPlaylistVideosAsync("UUaaaaaaaaaaaaaaaaaaaaaa", 2, Ct);

        Assert.Equal(2, videos.Count);
        Assert.Single(_http.Requests);
        Assert.Equal("2", Query(_http.Requests[0])["maxResults"]);
    }

    // ---- search ----

    [Fact]
    public async Task Search_decodes_html_records_100_units_and_caches()
    {
        _http.RespondFixture("search-two");
        var client = CreateClient();

        var first = await client.SearchVideosAsync(new SearchRequest("  messi   stats "), Ct);
        var second = await client.SearchVideosAsync(new SearchRequest("MESSI stats"), Ct);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Single(_http.Requests);
        Assert.Equal("Messi's craziest stat & more", first.Hits[0].Title);
        Assert.Equal(1_000_000, first.TotalResultsEstimate);

        var usage = Ledger.GetUsage();
        Assert.Equal(100, usage.UnitsUsed);
        Assert.Equal(1, usage.SearchCallsUsed);

        var query = Query(_http.Requests[0]);
        Assert.Equal("messi stats", query["q"]);
        Assert.Equal("video", query["type"]);
        Assert.Equal("25", query["maxResults"]);
    }

    [Fact]
    public async Task Search_passes_filters()
    {
        _http.RespondFixture("search-two");

        await CreateClient().SearchVideosAsync(
            new SearchRequest("ronaldo", MaxResults: 10, Order: SearchOrder.ViewCount,
                PublishedAfterUtc: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), RegionCode: "gb", Duration: SearchDuration.Short),
            Ct);

        var query = Query(_http.Requests[0]);
        Assert.Equal("viewCount", query["order"]);
        Assert.Equal("2026-11-01T00:00:00Z", query["publishedAfter"]);
        Assert.Equal("GB", query["regionCode"]);
        Assert.Equal("short", query["videoDuration"]);
        Assert.Equal("10", query["maxResults"]);
    }

    [Fact]
    public async Task Search_cap_blocks_without_calling_youtube()
    {
        _options.DailySearchCallCap = 1;
        _http.RespondFixture("search-two");
        var client = CreateClient();

        await client.SearchVideosAsync(new SearchRequest("one"), Ct);
        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => client.SearchVideosAsync(new SearchRequest("two"), Ct));

        Assert.Equal(YouTubeErrorKind.SearchCapReached, ex.Kind);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task Search_cap_resets_at_midnight_pacific()
    {
        _options.DailySearchCallCap = 1;
        _http.RespondFixture("search-two").RespondFixture("search-two");
        var client = CreateClient();

        await client.SearchVideosAsync(new SearchRequest("one"), Ct);
        _time.Advance(TimeSpan.FromDays(1));
        await client.SearchVideosAsync(new SearchRequest("two"), Ct);

        Assert.Equal(2, _http.Requests.Count);
    }

    [Fact]
    public async Task Local_quota_prediction_blocks_calls_that_would_exceed_the_limit()
    {
        _options.DailyQuotaUnits = 150;
        _http.RespondFixture("search-two");
        var client = CreateClient();

        await client.SearchVideosAsync(new SearchRequest("one"), Ct);
        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => client.SearchVideosAsync(new SearchRequest("two"), Ct));

        Assert.Equal(YouTubeErrorKind.QuotaExceeded, ex.Kind);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task Empty_search_query_is_invalid_input()
    {
        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => CreateClient().SearchVideosAsync(new SearchRequest("   "), Ct));
        Assert.Equal(YouTubeErrorKind.InvalidInput, ex.Kind);
    }

    // ---- errors ----

    [Fact]
    public async Task Missing_api_key_fails_before_any_request()
    {
        _options.ApiKey = null;

        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => CreateClient().GetVideosAsync(["aaaaaaaaaaa"], Ct));

        Assert.Equal(YouTubeErrorKind.ApiKeyMissing, ex.Kind);
        Assert.Empty(_http.Requests);
    }

    [Theory]
    [InlineData("error-quota", HttpStatusCode.Forbidden, YouTubeErrorKind.QuotaExceeded)]
    [InlineData("error-key-invalid", HttpStatusCode.BadRequest, YouTubeErrorKind.ApiKeyInvalid)]
    [InlineData("error-api-disabled", HttpStatusCode.Forbidden, YouTubeErrorKind.ApiKeyInvalid)]
    [InlineData("error-playlist-not-found", HttpStatusCode.NotFound, YouTubeErrorKind.NotFound)]
    public async Task Api_errors_map_to_kinds_and_are_not_retried(string fixture, HttpStatusCode status, YouTubeErrorKind expected)
    {
        _http.RespondFixture(fixture, status);

        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => CreateClient().GetPlaylistVideosAsync("UUx", 5, Ct));

        Assert.Equal(expected, ex.Kind);
        Assert.Single(_http.Requests);
        Assert.DoesNotContain(ApiKey, ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, Ledger.GetUsage().UnitsUsed); // failed calls still cost quota
    }

    [Fact]
    public async Task Transient_errors_are_retried_with_backoff()
    {
        _http.Respond(HttpStatusCode.ServiceUnavailable, "oops")
             .Throw(new HttpRequestException("connection reset"))
             .RespondFixture("videos-two");

        var task = CreateClient().GetVideosAsync(["aaaaaaaaaaa"], Ct);
        await AdvanceUntilDone(task);

        Assert.Equal(2, (await task).Videos.Count);
        Assert.Equal(3, _http.Requests.Count);
        Assert.Equal(3, Ledger.GetUsage().UnitsUsed);
    }

    [Fact]
    public async Task Transient_errors_give_up_after_max_retries()
    {
        _http.Respond(HttpStatusCode.InternalServerError, "{}")
             .Respond(HttpStatusCode.InternalServerError, "{}")
             .Respond(HttpStatusCode.InternalServerError, "{}");

        var task = CreateClient().GetVideosAsync(["aaaaaaaaaaa"], Ct);
        await AdvanceUntilDone(task);

        var ex = await Assert.ThrowsAsync<YouTubeApiException>(() => task);
        Assert.Equal(YouTubeErrorKind.Transient, ex.Kind);
        Assert.Equal(3, _http.Requests.Count); // 1 + MaxRetries(2)
    }

    private async Task AdvanceUntilDone(Task task)
    {
        for (var i = 0; i < 100 && !task.IsCompleted; i++)
        {
            await Task.Delay(5, Ct);
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(task.IsCompleted, "Request did not finish while advancing fake time.");
    }

    // ---- helpers ----

    [Theory]
    [InlineData("PT58S", 58)]
    [InlineData("PT3M", 180)]
    [InlineData("PT1H2M3S", 3723)]
    [InlineData("P0D", 0)]
    public void Durations_parse_from_iso8601(string iso, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), YouTubeDataClient.ParseDuration(iso));

    [Fact]
    public void Bad_duration_is_null() => Assert.Null(YouTubeDataClient.ParseDuration("nonsense"));
}
