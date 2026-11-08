namespace PersonalVidIQ.Domain.Enums;

/// <summary>Where a number came from. Every metric returned to Claude carries one of these.</summary>
public enum MetricSource
{
    /// <summary>Returned by the YouTube Data API as-is.</summary>
    YouTubeApi,

    /// <summary>Calculated by PersonalVidIQ from stored observations. Not a YouTube metric.</summary>
    Calculated,

    /// <summary>An estimate from a third-party provider.</summary>
    ThirdPartyEstimate,

    /// <summary>Not available (hidden, not collected yet, or not obtainable).</summary>
    Unavailable,
}
