using System.ComponentModel.DataAnnotations;

namespace PersonalVidIQ.Application.Options;

public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";

    /// <summary>
    /// YouTube Data API key. Optional at startup so the MCP server can still start and report
    /// what is missing; YouTube calls fail with a clear message until it is set.
    /// </summary>
    public string? ApiKey { get; set; }

    [Required, Url]
    public string BaseUrl { get; set; } = "https://www.googleapis.com/youtube/v3/";

    [Range(1, 50)]
    public int MaxSearchResults { get; set; } = 25;

    /// <summary>Daily quota granted to the Google Cloud project (default allocation is 10,000 units).</summary>
    [Range(1, 1_000_000)]
    public int DailyQuotaUnits { get; set; } = 10_000;

    /// <summary>Maximum search.list calls per day (100 units each). DECISIONS.md #8.</summary>
    [Range(0, 10_000)]
    public int DailySearchCallCap { get; set; } = 50;

    /// <summary>How long a search result is reused for the same normalised query. DECISIONS.md #8.</summary>
    [Range(0, 168)]
    public int SearchCacheHours { get; set; } = 24;
}
