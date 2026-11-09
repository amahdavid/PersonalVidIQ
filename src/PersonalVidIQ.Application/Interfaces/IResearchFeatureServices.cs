using PersonalVidIQ.Application.Research;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Interfaces;

public interface IKeywordResearchService
{
    /// <summary>What ranks for a keyword on YouTube search and how crowded it is. About 102 quota units (search cached 24 h).</summary>
    Task<KeywordReport> ResearchKeywordAsync(string keyword, int? maxResults, string? regionCode, CancellationToken cancellationToken);

    /// <summary>Keywords and topics from tracked channels' stored videos, ranked by channel-relative performance. No quota.</summary>
    Task<KeywordIdeasReport> GetKeywordIdeasAsync(string? seed, VideoFormat? format, CancellationToken cancellationToken);
}

public interface ITrendResearchService
{
    /// <summary>Keywords rising across tracked channels in the recent window. No quota.</summary>
    Task<TrendReport> DiscoverTrendsAsync(int windowDays, VideoFormat? format, CancellationToken cancellationToken);

    /// <summary>Recent uploads across tracked channels with early performance. No quota.</summary>
    Task<RecentUploadsReport> GetRecentUploadsAsync(int hours, CancellationToken cancellationToken);

    /// <summary>Evidence bundle for proposing video ideas. No quota unless myChannel isn't stored (about 2 units).</summary>
    Task<IdeaEvidenceReport> GetVideoIdeaEvidenceAsync(string? myChannel, int windowDays, CancellationToken cancellationToken);
}
