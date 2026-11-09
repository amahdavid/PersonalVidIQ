using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Research;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class ResearchTools(IKeywordResearchService keywords, ITrendResearchService trends)
{
    [McpServerTool(Name = "research_keyword", Title = "Research a keyword", ReadOnly = true, OpenWorld = true)]
    [Description("See what ranks on YouTube search for a keyword and how crowded it is: median views and views/day of top results, how fresh they are, " +
                 "Shorts share, how many results target the exact wording, how many channels and how big they are, and related terms from result titles and tags. " +
                 "Never search volume (YouTube doesn't publish it). About 102 quota units; repeating the same keyword within 24 h costs 2.")]
    public Task<KeywordReport> ResearchKeyword(
        [Description("The keyword or phrase.")] string keyword,
        [Description("Results to analyse, 1-25. Default 25.")] int? maxResults = null,
        [Description("Two-letter region code, e.g. GB or US.")] string? regionCode = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => keywords.ResearchKeywordAsync(keyword, maxResults, regionCode, cancellationToken));

    [McpServerTool(Name = "get_keyword_ideas", Title = "Keyword ideas from tracked channels", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Keywords and topics from tracked channels' stored videos, ranked by how videos using them did against their own channel's usual " +
                 "(1.0 = typical). Give a seed to see what's used alongside it. Also groups keywords into topics. No quota; run refresh_tracked first for fresh numbers.")]
    public Task<KeywordIdeasReport> GetKeywordIdeas(
        [Description("Optional seed keyword or phrase to focus on.")] string? seed = null,
        [Description("Optional: short or longform.")] string? format = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => keywords.GetKeywordIdeasAsync(seed, ChannelTools.ParseFormat(format), cancellationToken));

    [McpServerTool(Name = "discover_trends", Title = "Discover emerging topics", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Keywords emerging across tracked channels: used noticeably more in the recent window than the one before, and/or recent videos using them " +
                 "beating their channels' usual. Needs several videos (and 2+ channels when 2+ are tracked); never from a single video. No quota.")]
    public Task<TrendReport> DiscoverTrends(
        [Description("Recent window in days, 1-15, compared with the same length before it. Default 7.")] int? windowDays = null,
        [Description("Optional: short or longform.")] string? format = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => trends.DiscoverTrendsAsync(windowDays ?? 7, ChannelTools.ParseFormat(format), cancellationToken));

    [McpServerTool(Name = "get_recent_uploads", Title = "Recent uploads from tracked channels", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("What tracked channels uploaded recently, newest first, with views/day, last-24 h velocity where history exists, and how each compares " +
                 "with its channel's usual. No quota; run refresh_tracked first to pick up the newest uploads.")]
    public Task<RecentUploadsReport> GetRecentUploads(
        [Description("Look back this many hours, 1-720. Default 48.")] int? hours = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => trends.GetRecentUploadsAsync(hours ?? 48, cancellationToken));

    [McpServerTool(Name = "get_video_idea_evidence", Title = "Evidence for video ideas", ReadOnly = true, OpenWorld = true)]
    [Description("Evidence to base video ideas on, from tracked channels: recent outlier videos, best-performing keywords, emerging keywords, and (with myChannel) " +
                 "keyword gaps that do well for competitors but that your channel hasn't used. PersonalVidIQ doesn't write the ideas; propose them from this " +
                 "evidence and cite it. No quota if myChannel is tracked or stored, otherwise about 2 units.")]
    public Task<IdeaEvidenceReport> GetVideoIdeaEvidence(
        [Description("Optional: your channel (@handle, UC… id or URL), to exclude it from competitor evidence and find keyword gaps.")] string? myChannel = null,
        [Description("Window for emerging keywords in days, 1-15. Default 7.")] int? windowDays = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => trends.GetVideoIdeaEvidenceAsync(myChannel, windowDays ?? 7, cancellationToken));
}
