using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class SearchTools(IResearchService research, TimeProvider timeProvider)
{
    [McpServerTool(Name = "search_youtube", Title = "Search YouTube videos", ReadOnly = true, OpenWorld = true)]
    [Description("Search YouTube for videos on a topic and get each result's public stats, Short vs long-form, and lifetime views/day. " +
                 "Expensive: 100 quota units per new search (capped at 50 a day); repeating the same search within 24 h is free. " +
                 "Results are YouTube's ranking, not search volume. Prefer get_channel_videos or find_outliers when you already know the channel.")]
    public Task<SearchReport> SearchYouTube(
        [Description("What to search for, e.g. 'messi stats'.")] string query,
        [Description("Number of results, 1-25. Default 25.")] int? maxResults = null,
        [Description("relevance (default), date, viewcount or rating.")] string? order = null,
        [Description("Only videos published in the last N days (1-365).")] int? publishedWithinDays = null,
        [Description("Two-letter region code, e.g. GB or US.")] string? regionCode = null,
        [Description("YouTube's duration filter: any (default), short (<4 min, not the same as Shorts), medium (4-20 min), long (>20 min).")] string? duration = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => research.SearchAsync(
            new SearchRequest(
                query,
                maxResults,
                ToolGuard.ParseEnum(order, SearchOrder.Relevance, "order"),
                publishedWithinDays is { } days ? timeProvider.GetUtcNow().AddDays(-Math.Clamp(days, 1, 365)) : null,
                regionCode,
                ToolGuard.ParseEnum(duration, SearchDuration.Any, "duration")),
            cancellationToken));
}
