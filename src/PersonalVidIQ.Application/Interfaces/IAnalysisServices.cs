using PersonalVidIQ.Application.DTOs.Analysis;

namespace PersonalVidIQ.Application.Interfaces;

public interface IVideoAnalysisService
{
    /// <summary>Public stats, calculated rates and velocity, stored history and a channel-relative outlier score. About 4 quota units.</summary>
    Task<VideoAnalysis> AnalyzeVideoAsync(string video, CancellationToken cancellationToken);

    /// <summary>Side-by-side stats and calculated metrics for up to 10 videos (1 quota unit). No outlier scores.</summary>
    Task<VideoComparison> CompareVideosAsync(IReadOnlyList<string> videos, CancellationToken cancellationToken);

    /// <summary>Scores a channel's recent uploads against its own baseline. About 3 quota units.</summary>
    Task<OutlierReport> FindOutliersAsync(OutlierQuery query, CancellationToken cancellationToken);
}

public interface IChannelResearchService
{
    /// <summary>Compares up to 5 channels over the last 1-30 days. About 3 quota units per channel.</summary>
    Task<ChannelComparison> CompareChannelsAsync(IReadOnlyList<string> channels, int periodDays, CancellationToken cancellationToken);
}
