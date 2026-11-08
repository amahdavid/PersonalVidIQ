namespace PersonalVidIQ.Application.YouTube;

public enum YouTubeErrorKind
{
    /// <summary>No API key configured.</summary>
    ApiKeyMissing,

    /// <summary>The key was rejected or the API is not enabled for its project.</summary>
    ApiKeyInvalid,

    /// <summary>YouTube reports the daily quota is used up, or the local ledger predicts it would be.</summary>
    QuotaExceeded,

    /// <summary>The local daily search cap (DECISIONS.md #8) is reached.</summary>
    SearchCapReached,

    /// <summary>The channel, video or playlist does not exist or is not public.</summary>
    NotFound,

    /// <summary>The input could not be understood (bad id, URL or parameter).</summary>
    InvalidInput,

    /// <summary>Network failure, 5xx or rate limiting that persisted after retries.</summary>
    Transient,

    /// <summary>Any other API error.</summary>
    Other,
}

/// <summary>A YouTube Data API failure with a message that is safe to show the user (never contains the key).</summary>
public sealed class YouTubeApiException(YouTubeErrorKind kind, string message, string? reason = null, Exception? inner = null)
    : Exception(message, inner)
{
    public YouTubeErrorKind Kind { get; } = kind;

    /// <summary>The "reason" string from YouTube's error payload, when there was one.</summary>
    public string? Reason { get; } = reason;
}
