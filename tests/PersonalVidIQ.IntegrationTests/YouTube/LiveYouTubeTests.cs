using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Infrastructure.YouTube;

namespace PersonalVidIQ.IntegrationTests.YouTube;

/// <summary>
/// Calls the real YouTube Data API. Skipped unless PERSONALVIDIQ_YOUTUBE_API_KEY is set.
/// Costs about 3 quota units per run (no search.list).
/// </summary>
public sealed class LiveYouTubeTests
{
    private static YouTubeDataClient CreateClient()
    {
        var key = Environment.GetEnvironmentVariable("PERSONALVIDIQ_YOUTUBE_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Skip("Set PERSONALVIDIQ_YOUTUBE_API_KEY to run live YouTube tests.");
        }

        var options = Options.Create(new YouTubeOptions { ApiKey = key });
        return new YouTubeDataClient(
            new HttpClient { BaseAddress = new Uri(options.Value.BaseUrl) },
            options,
            new InMemoryQuotaLedger(options, TimeProvider.System),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLogger<YouTubeDataClient>.Instance);
    }

    [Fact]
    public async Task Handle_resolves_and_uploads_playlist_and_videos_load()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var channel = await client.GetChannelAsync(YouTubeInput.ParseChannel("@YouTube"), ct);
        Assert.StartsWith("UC", channel.Id, StringComparison.Ordinal);
        Assert.NotNull(channel.UploadsPlaylistId);

        var uploads = await client.GetPlaylistVideosAsync(channel.UploadsPlaylistId!, 3, ct);
        Assert.NotEmpty(uploads);

        var batch = await client.GetVideosAsync(uploads.Select(u => u.VideoId).ToList(), ct);
        Assert.NotEmpty(batch.Videos);
        Assert.All(batch.Videos, v => Assert.Equal(channel.Id, v.ChannelId));
    }
}
