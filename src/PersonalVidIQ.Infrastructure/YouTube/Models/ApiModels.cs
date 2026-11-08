using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalVidIQ.Infrastructure.YouTube.Models;

// Wire models for the YouTube Data API v3. Only the fields PersonalVidIQ uses.
// Counts arrive as JSON strings ("12345"); the serializer is configured to read numbers from strings.

internal sealed record ListResponse<T>(string? NextPageToken, PageInfo? PageInfo, List<T>? Items);

internal sealed record PageInfo(long? TotalResults, int? ResultsPerPage);

internal sealed record Thumbnail(string? Url, int? Width, int? Height);

internal sealed record ChannelItem(string Id, ChannelSnippet? Snippet, ChannelStatistics? Statistics, ChannelContentDetails? ContentDetails);

internal sealed record ChannelSnippet(
    string? Title,
    string? Description,
    string? CustomUrl,
    DateTimeOffset? PublishedAt,
    Dictionary<string, Thumbnail>? Thumbnails,
    string? Country);

internal sealed record ChannelStatistics(long? ViewCount, long? SubscriberCount, bool HiddenSubscriberCount, long? VideoCount);

internal sealed record ChannelContentDetails(RelatedPlaylists? RelatedPlaylists);

internal sealed record RelatedPlaylists(string? Uploads);

internal sealed record VideoItem(string Id, VideoSnippet? Snippet, VideoContentDetails? ContentDetails, VideoStatistics? Statistics);

internal sealed record VideoSnippet(
    DateTimeOffset? PublishedAt,
    string? ChannelId,
    string? Title,
    string? Description,
    Dictionary<string, Thumbnail>? Thumbnails,
    string? ChannelTitle,
    List<string>? Tags,
    string? CategoryId,
    string? LiveBroadcastContent);

internal sealed record VideoContentDetails(string? Duration);

internal sealed record VideoStatistics(long? ViewCount, long? LikeCount, long? CommentCount);

internal sealed record SearchItem(SearchItemId? Id, SearchSnippet? Snippet);

internal sealed record SearchItemId(string? Kind, string? VideoId);

internal sealed record SearchSnippet(
    DateTimeOffset? PublishedAt,
    string? ChannelId,
    string? Title,
    string? Description,
    Dictionary<string, Thumbnail>? Thumbnails,
    string? ChannelTitle);

internal sealed record PlaylistItem(PlaylistItemSnippet? Snippet, PlaylistItemContentDetails? ContentDetails);

internal sealed record PlaylistItemSnippet(string? Title);

internal sealed record PlaylistItemContentDetails(string? VideoId, DateTimeOffset? VideoPublishedAt);

internal sealed record ErrorResponse(ErrorBody? Error);

internal sealed record ErrorBody(int Code, string? Message, List<ErrorDetail>? Errors);

internal sealed record ErrorDetail(string? Reason, string? Domain, string? Message);

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ListResponse<ChannelItem>))]
[JsonSerializable(typeof(ListResponse<VideoItem>))]
[JsonSerializable(typeof(ListResponse<SearchItem>))]
[JsonSerializable(typeof(ListResponse<PlaylistItem>))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class YouTubeJsonContext : JsonSerializerContext;
