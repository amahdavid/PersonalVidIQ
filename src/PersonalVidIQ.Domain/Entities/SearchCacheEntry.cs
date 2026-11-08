namespace PersonalVidIQ.Domain.Entities;

/// <summary>A cached search.list result, kept for the search cache window (default 24 h).</summary>
public sealed class SearchCacheEntry
{
    public required string Key { get; set; }

    public required string ResultJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
