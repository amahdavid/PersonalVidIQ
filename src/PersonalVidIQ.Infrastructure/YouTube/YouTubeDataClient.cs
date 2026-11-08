using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Infrastructure.YouTube.Models;

namespace PersonalVidIQ.Infrastructure.YouTube;

/// <summary>
/// YouTube Data API v3 client for public data. Centralises quota accounting, the search cap,
/// search caching, retries and error mapping. The API key is never logged or put in exceptions.
/// </summary>
public sealed class YouTubeDataClient(
    HttpClient http,
    IOptions<YouTubeOptions> options,
    IQuotaLedger quotaLedger,
    ISearchCache cache,
    TimeProvider timeProvider,
    ILogger<YouTubeDataClient> logger) : IYouTubeDataClient
{
    private const int MaxIdsPerCall = 50;
    private const int MaxPlaylistVideos = 500;

    private static readonly string[] ThumbnailPreference = ["maxres", "standard", "high", "medium", "default"];

    private YouTubeOptions Options => options.Value;

    public async Task<SearchResult> SearchVideosAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var query = NormaliseQuery(request.Query);
        if (query.Length == 0)
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, "Search query is empty.");
        }

        var maxResults = Math.Clamp(request.MaxResults ?? Options.MaxSearchResults, 1, Options.MaxSearchResults);
        var normalised = request with { Query = query, MaxResults = maxResults };
        var cacheKey = SearchCacheKey(normalised);

        var cached = Options.SearchCacheHours > 0 ? await cache.GetAsync(cacheKey, cancellationToken) : null;
        if (cached is not null)
        {
            logger.LogInformation("search.list served from cache (0 units)");
            return cached with { FromCache = true };
        }

        var usage = await quotaLedger.GetUsageAsync(cancellationToken);
        if (usage.SearchCallsUsed >= usage.SearchCallCap)
        {
            throw new YouTubeApiException(
                YouTubeErrorKind.SearchCapReached,
                $"Daily search cap reached ({usage.SearchCallsUsed}/{usage.SearchCallCap} searches, 100 units each). " +
                $"It resets at {usage.QuotaDayResetsAtUtc:yyyy-MM-dd HH:mm} UTC. Channel and video lookups still work.");
        }

        var parameters = new Dictionary<string, string?>
        {
            ["part"] = "snippet",
            ["type"] = "video",
            ["q"] = query,
            ["maxResults"] = maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["order"] = request.Order switch
            {
                SearchOrder.Date => "date",
                SearchOrder.ViewCount => "viewCount",
                SearchOrder.Rating => "rating",
                _ => "relevance",
            },
            ["publishedAfter"] = request.PublishedAfterUtc?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["regionCode"] = string.IsNullOrWhiteSpace(request.RegionCode) ? null : request.RegionCode.Trim().ToUpperInvariant(),
            ["videoDuration"] = request.Duration switch
            {
                SearchDuration.Short => "short",
                SearchDuration.Medium => "medium",
                SearchDuration.Long => "long",
                _ => null,
            },
        };

        var response = await GetAsync("search.list", parameters, YouTubeJsonContext.Default.ListResponseSearchItem, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var hits = (response.Items ?? [])
            .Where(i => !string.IsNullOrEmpty(i.Id?.VideoId))
            .Select(i => new SearchHit(
                i.Id!.VideoId!,
                i.Snippet?.ChannelId ?? string.Empty,
                Decode(i.Snippet?.ChannelTitle),
                Decode(i.Snippet?.Title),
                Decode(i.Snippet?.Description),
                i.Snippet?.PublishedAt?.ToUniversalTime(),
                BestThumbnail(i.Snippet?.Thumbnails)))
            .ToList();

        var result = new SearchResult(normalised, hits, response.PageInfo?.TotalResults, FromCache: false, now);

        if (Options.SearchCacheHours > 0)
        {
            await cache.SetAsync(cacheKey, result, TimeSpan.FromHours(Options.SearchCacheHours), cancellationToken);
        }

        return result;
    }

    public async Task<IReadOnlyList<ChannelData>> GetChannelsAsync(IReadOnlyCollection<string> channelIds, CancellationToken cancellationToken)
    {
        var ids = channelIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var channels = new List<ChannelData>(ids.Count);

        foreach (var batch in ids.Chunk(MaxIdsPerCall))
        {
            var response = await GetAsync(
                "channels.list",
                new Dictionary<string, string?> { ["part"] = "snippet,statistics,contentDetails", ["id"] = string.Join(',', batch), ["maxResults"] = "50" },
                YouTubeJsonContext.Default.ListResponseChannelItem,
                cancellationToken);
            var now = timeProvider.GetUtcNow();
            channels.AddRange((response.Items ?? []).Select(c => MapChannel(c, now)));
        }

        return channels;
    }

    public async Task<ChannelData> GetChannelAsync(ChannelRef channel, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?> { ["part"] = "snippet,statistics,contentDetails" };
        switch (channel.Kind)
        {
            case ChannelRefKind.Id:
                parameters["id"] = channel.Value;
                break;
            case ChannelRefKind.Handle:
                parameters["forHandle"] = channel.Value;
                break;
            case ChannelRefKind.Username:
                parameters["forUsername"] = channel.Value;
                break;
        }

        var response = await GetAsync("channels.list", parameters, YouTubeJsonContext.Default.ListResponseChannelItem, cancellationToken);
        var item = response.Items?.FirstOrDefault()
            ?? throw new YouTubeApiException(YouTubeErrorKind.NotFound, $"No channel found for {Describe(channel)}.");

        return MapChannel(item, timeProvider.GetUtcNow());
    }

    public async Task<VideoBatch> GetVideosAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken)
    {
        var ids = videoIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var videos = new List<VideoData>(ids.Count);

        foreach (var batch in ids.Chunk(MaxIdsPerCall))
        {
            var response = await GetAsync(
                "videos.list",
                new Dictionary<string, string?> { ["part"] = "snippet,contentDetails,statistics", ["id"] = string.Join(',', batch), ["maxResults"] = "50" },
                YouTubeJsonContext.Default.ListResponseVideoItem,
                cancellationToken);
            var now = timeProvider.GetUtcNow();
            videos.AddRange((response.Items ?? []).Select(v => MapVideo(v, now)));
        }

        var found = videos.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
        return new VideoBatch(videos, ids.Where(id => !found.Contains(id)).ToList());
    }

    public async Task<IReadOnlyList<PlaylistVideo>> GetPlaylistVideosAsync(string playlistId, int maxResults, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            throw new YouTubeApiException(YouTubeErrorKind.InvalidInput, "Playlist id is empty.");
        }

        var limit = Math.Clamp(maxResults, 1, MaxPlaylistVideos);
        var videos = new List<PlaylistVideo>(limit);
        string? pageToken = null;

        do
        {
            var response = await GetAsync(
                "playlistItems.list",
                new Dictionary<string, string?>
                {
                    ["part"] = "snippet,contentDetails",
                    ["playlistId"] = playlistId.Trim(),
                    ["maxResults"] = Math.Min(MaxIdsPerCall, limit - videos.Count).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["pageToken"] = pageToken,
                },
                YouTubeJsonContext.Default.ListResponsePlaylistItem,
                cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                var videoId = item.ContentDetails?.VideoId;
                if (!string.IsNullOrEmpty(videoId) && videos.Count < limit)
                {
                    videos.Add(new PlaylistVideo(videoId, item.Snippet?.Title ?? string.Empty, item.ContentDetails?.VideoPublishedAt?.ToUniversalTime()));
                }
            }

            pageToken = response.NextPageToken;
        }
        while (pageToken is not null && videos.Count < limit);

        return videos;
    }

    private async Task<T> GetAsync<T>(
        string endpoint,
        IReadOnlyDictionary<string, string?> parameters,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        var apiKey = Options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new YouTubeApiException(
                YouTubeErrorKind.ApiKeyMissing,
                "YouTube API key is not set. Run: dotnet user-secrets set \"YouTube:ApiKey\" \"<key>\" --project src/PersonalVidIQ.Host");
        }

        var cost = Options.CostOf(endpoint);
        var usage = await quotaLedger.GetUsageAsync(cancellationToken);
        if (usage.UnitsUsed + cost > usage.DailyUnitLimit)
        {
            throw new YouTubeApiException(
                YouTubeErrorKind.QuotaExceeded,
                $"This call ({endpoint}, {cost} units) would go over the daily quota ({usage.UnitsUsed}/{usage.DailyUnitLimit} used). " +
                $"It resets at {usage.QuotaDayResetsAtUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        var relativeUrl = BuildRelativeUrl(endpoint, parameters, apiKey);

        for (var attempt = 0; ; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            await quotaLedger.RecordAsync(endpoint, cost, cancellationToken);
            YouTubeApiException failure;

            try
            {
                using var response = await http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                logger.LogInformation(
                    "YouTube {Endpoint} -> {Status} in {ElapsedMs} ms ({Cost} units, attempt {Attempt})",
                    endpoint, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, cost, attempt + 1);

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
                        ?? throw new YouTubeApiException(YouTubeErrorKind.Other, $"YouTube {endpoint} returned an empty body.");
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                failure = MapError(endpoint, response.StatusCode, body);
            }
            catch (HttpRequestException ex)
            {
                failure = new YouTubeApiException(YouTubeErrorKind.Transient, $"Network error calling YouTube {endpoint}: {ex.Message}", inner: ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                failure = new YouTubeApiException(YouTubeErrorKind.Transient, $"YouTube {endpoint} timed out.", inner: ex);
            }
            catch (JsonException ex)
            {
                throw new YouTubeApiException(YouTubeErrorKind.Other, $"YouTube {endpoint} returned JSON PersonalVidIQ couldn't read: {ex.Message}", inner: ex);
            }

            if (failure.Kind != YouTubeErrorKind.Transient || attempt >= Options.MaxRetries)
            {
                logger.LogWarning("YouTube {Endpoint} failed: {Kind} {Reason}", endpoint, failure.Kind, failure.Reason);
                throw failure;
            }

            // Exponential backoff: 0.5 s, 1 s, 2 s, …
            await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt)), timeProvider, cancellationToken);
        }
    }

    private static string BuildRelativeUrl(string endpoint, IReadOnlyDictionary<string, string?> parameters, string apiKey)
    {
        var path = endpoint.Split('.')[0];
        var builder = new StringBuilder(path).Append('?');
        foreach (var (key, value) in parameters)
        {
            if (value is not null)
            {
                builder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value)).Append('&');
            }
        }

        return builder.Append("key=").Append(Uri.EscapeDataString(apiKey)).ToString();
    }

    internal static YouTubeApiException MapError(string endpoint, HttpStatusCode status, string body)
    {
        ErrorBody? error = null;
        try
        {
            error = JsonSerializer.Deserialize(body, YouTubeJsonContext.Default.ErrorResponse)?.Error;
        }
        catch (JsonException)
        {
            // Non-JSON error page; fall back to the status code.
        }

        var reason = error?.Errors?.FirstOrDefault()?.Reason;
        var message = error?.Message ?? status.ToString();
        var code = (int)status;

        if (reason is "quotaExceeded" or "dailyLimitExceeded")
        {
            return new YouTubeApiException(YouTubeErrorKind.QuotaExceeded,
                "YouTube says today's API quota is used up. It resets at midnight Pacific Time.", reason);
        }

        if (reason is "keyInvalid" || (code == 400 && message.Contains("API key", StringComparison.OrdinalIgnoreCase)))
        {
            return new YouTubeApiException(YouTubeErrorKind.ApiKeyInvalid,
                "YouTube rejected the API key. Check YouTube:ApiKey in user secrets.", reason ?? "keyInvalid");
        }

        if (reason is "accessNotConfigured" or "SERVICE_DISABLED" || message.Contains("has not been used in project", StringComparison.OrdinalIgnoreCase))
        {
            return new YouTubeApiException(YouTubeErrorKind.ApiKeyInvalid,
                "The YouTube Data API v3 isn't enabled for this key's Google Cloud project. Enable it in the Cloud console.", reason);
        }

        if (code is 429 or >= 500 || reason is "rateLimitExceeded" or "userRateLimitExceeded" or "backendError")
        {
            return new YouTubeApiException(YouTubeErrorKind.Transient, $"YouTube {endpoint} is temporarily unavailable ({code}).", reason);
        }

        if (code == 404 || (reason?.EndsWith("NotFound", StringComparison.Ordinal) ?? false))
        {
            return new YouTubeApiException(YouTubeErrorKind.NotFound, $"YouTube {endpoint}: not found ({reason ?? "404"}).", reason);
        }

        if (code == 400)
        {
            return new YouTubeApiException(YouTubeErrorKind.InvalidInput, $"YouTube {endpoint} rejected the request: {message}", reason);
        }

        return new YouTubeApiException(YouTubeErrorKind.Other, $"YouTube {endpoint} failed ({code}): {message}", reason);
    }

    private static ChannelData MapChannel(ChannelItem c, DateTimeOffset retrievedAt) => new(
        c.Id,
        c.Snippet?.Title ?? string.Empty,
        c.Snippet?.Description ?? string.Empty,
        c.Snippet?.CustomUrl,
        BestThumbnail(c.Snippet?.Thumbnails),
        c.Snippet?.PublishedAt?.ToUniversalTime(),
        c.Snippet?.Country,
        c.ContentDetails?.RelatedPlaylists?.Uploads,
        c.Statistics is { HiddenSubscriberCount: true } ? null : c.Statistics?.SubscriberCount,
        c.Statistics?.HiddenSubscriberCount ?? false,
        c.Statistics?.ViewCount,
        c.Statistics?.VideoCount,
        retrievedAt);

    private static VideoData MapVideo(VideoItem v, DateTimeOffset retrievedAt) => new(
        v.Id,
        v.Snippet?.ChannelId ?? string.Empty,
        v.Snippet?.ChannelTitle ?? string.Empty,
        v.Snippet?.Title ?? string.Empty,
        v.Snippet?.Description ?? string.Empty,
        v.Snippet?.PublishedAt?.ToUniversalTime(),
        ParseDuration(v.ContentDetails?.Duration),
        BestThumbnail(v.Snippet?.Thumbnails),
        v.Snippet?.Tags ?? [],
        v.Snippet?.CategoryId,
        v.Snippet?.LiveBroadcastContent,
        v.Statistics?.ViewCount,
        v.Statistics?.LikeCount,
        v.Statistics?.CommentCount,
        retrievedAt);

    internal static TimeSpan? ParseDuration(string? iso8601)
    {
        if (string.IsNullOrWhiteSpace(iso8601))
        {
            return null;
        }

        try
        {
            return XmlConvert.ToTimeSpan(iso8601);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? BestThumbnail(Dictionary<string, Thumbnail>? thumbnails)
    {
        if (thumbnails is null)
        {
            return null;
        }

        foreach (var key in ThumbnailPreference)
        {
            if (thumbnails.TryGetValue(key, out var thumb) && !string.IsNullOrEmpty(thumb.Url))
            {
                return thumb.Url;
            }
        }

        return null;
    }

    // search.list returns HTML-escaped snippets (&#39;, &amp;); videos.list does not.
    private static string Decode(string? value) => WebUtility.HtmlDecode(value ?? string.Empty);

    private static string NormaliseQuery(string? query) =>
        string.Join(' ', (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string SearchCacheKey(SearchRequest r) =>
        $"search|{r.Query.ToLowerInvariant()}|{r.MaxResults}|{r.Order}|{r.PublishedAfterUtc?.UtcTicks}|{r.RegionCode?.ToUpperInvariant()}|{r.Duration}";

    private static string Describe(ChannelRef channel) => channel.Kind switch
    {
        ChannelRefKind.Handle => channel.Value,
        ChannelRefKind.Username => $"username '{channel.Value}'",
        _ => $"id {channel.Value}",
    };
}
