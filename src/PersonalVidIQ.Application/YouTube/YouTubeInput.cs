using System.Text.RegularExpressions;

namespace PersonalVidIQ.Application.YouTube;

public enum ChannelRefKind
{
    /// <summary>A UC… channel id.</summary>
    Id,

    /// <summary>An @handle, resolved with channels.list forHandle.</summary>
    Handle,

    /// <summary>A legacy /user/ name, resolved with channels.list forUsername.</summary>
    Username,
}

public sealed record ChannelRef(ChannelRefKind Kind, string Value);

/// <summary>Turns what a user pastes (ids, @handles, URLs) into API identifiers.</summary>
public static partial class YouTubeInput
{
    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}$")]
    private static partial Regex ChannelIdRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdRegex();

    [GeneratedRegex(@"^@?[\p{L}\p{N}._-]{3,100}$")]
    private static partial Regex HandleRegex();

    /// <summary>
    /// Accepts "UC…", "@handle", "handle" (with @ implied only when prefixed), and URLs such as
    /// youtube.com/channel/UC…, youtube.com/@handle, youtube.com/user/name.
    /// Legacy /c/ custom URLs are not resolvable through the API and are rejected with a hint.
    /// </summary>
    public static ChannelRef ParseChannel(string input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw Invalid("Channel is empty. Pass a channel id (UC…), an @handle or a channel URL.");
        }

        if (ChannelIdRegex().IsMatch(value))
        {
            return new ChannelRef(ChannelRefKind.Id, value);
        }

        if (value.StartsWith('@'))
        {
            return HandleRef(value);
        }

        if (TryParseUrl(value, out var uri) && IsYouTubeHost(uri.Host))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                var first = Uri.UnescapeDataString(segments[0]);
                if (first.StartsWith('@'))
                {
                    return HandleRef(first);
                }

                if (segments.Length > 1)
                {
                    var second = Uri.UnescapeDataString(segments[1]);
                    switch (first.ToLowerInvariant())
                    {
                        case "channel" when ChannelIdRegex().IsMatch(second):
                            return new ChannelRef(ChannelRefKind.Id, second);
                        case "user":
                            return new ChannelRef(ChannelRefKind.Username, second);
                        case "c":
                            throw Invalid($"Legacy custom URLs (/c/{second}) can't be looked up through the YouTube API. Open the channel and use its @handle or /channel/UC… URL instead.");
                    }
                }
            }

            throw Invalid($"'{value}' is a YouTube URL but not a channel URL. Use youtube.com/@handle or youtube.com/channel/UC….");
        }

        throw Invalid($"'{value}' isn't a channel id, @handle or channel URL. Handles need the @, e.g. @channelname.");
    }

    /// <summary>
    /// Accepts an 11-character video id or a URL: youtube.com/watch?v=, youtu.be/, /shorts/, /live/, /embed/.
    /// </summary>
    public static string ParseVideoId(string input)
    {
        var value = (input ?? string.Empty).Trim();
        if (VideoIdRegex().IsMatch(value))
        {
            return value;
        }

        if (TryParseUrl(value, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (host is "youtu.be" or "www.youtu.be" && segments.Length > 0 && VideoIdRegex().IsMatch(segments[0]))
            {
                return segments[0];
            }

            if (IsYouTubeHost(host))
            {
                var v = QueryValue(uri, "v");
                if (v is not null && VideoIdRegex().IsMatch(v))
                {
                    return v;
                }

                if (segments.Length > 1
                    && segments[0].ToLowerInvariant() is "shorts" or "live" or "embed" or "v"
                    && VideoIdRegex().IsMatch(segments[1]))
                {
                    return segments[1];
                }
            }
        }

        throw Invalid($"'{value}' isn't a video id or video URL.");
    }

    private static ChannelRef HandleRef(string value)
    {
        if (!HandleRegex().IsMatch(value))
        {
            throw Invalid($"'{value}' isn't a valid @handle.");
        }

        return new ChannelRef(ChannelRefKind.Handle, value.StartsWith('@') ? value : "@" + value);
    }

    private static bool TryParseUrl(string value, out Uri uri)
    {
        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;
        return Uri.TryCreate(candidate, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && uri.Host.Contains('.', StringComparison.Ordinal);
    }

    private static bool IsYouTubeHost(string host)
    {
        host = host.ToLowerInvariant();
        return host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com";
    }

    private static string? QueryValue(Uri uri, string key)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    private static YouTubeApiException Invalid(string message) => new(YouTubeErrorKind.InvalidInput, message);
}
