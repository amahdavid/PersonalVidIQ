using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class VideoTools(IResearchService research, IVideoAnalysisService analysis)
{
    private const string VideoParam = "Video id or URL (watch?v=, youtu.be/, /shorts/).";

    [McpServerTool(Name = "get_video", Title = "Get video details", ReadOnly = true, OpenWorld = true)]
    [Description("Get a video's metadata, public stats, lifetime views/day and like/comment rates. 1 quota unit. Use analyze_video for velocity and an outlier score.")]
    public Task<VideoReport> GetVideo(
        [Description(VideoParam)] string video,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => research.GetVideoAsync(video, cancellationToken));

    [McpServerTool(Name = "analyze_video", Title = "Analyse a video", ReadOnly = true, OpenWorld = true)]
    [Description("Full analysis of one video: public stats, calculated views/day (lifetime, last 24 h, last 7 d from stored snapshots), " +
                 "engagement rates, and how it compares with the same channel's videos of the same format and age (outlier score). " +
                 "Every calculated number includes its formula, window, sample size and confidence. About 4 quota units.")]
    public Task<VideoAnalysis> AnalyzeVideo(
        [Description(VideoParam)] string video,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analysis.AnalyzeVideoAsync(video, cancellationToken));

    [McpServerTool(Name = "compare_videos", Title = "Compare videos", ReadOnly = true, OpenWorld = true)]
    [Description("Side-by-side public stats and calculated metrics for up to 10 videos. 1 quota unit. No outlier scores (use find_outliers).")]
    public Task<VideoComparison> CompareVideos(
        [Description("1-10 video ids or URLs.")] string[] videos,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analysis.CompareVideosAsync(videos, cancellationToken));

    [McpServerTool(Name = "find_outliers", Title = "Find outlier videos", ReadOnly = true, OpenWorld = true)]
    [Description("Find a channel's recent videos that are outperforming its own baseline: views/day compared with the same channel's " +
                 "videos of the same format (Shorts vs long-form) and similar age. Uses recent velocity from snapshots when available, " +
                 "otherwise lifetime views/day. About 3 quota units.")]
    public Task<OutlierReport> FindOutliers(
        [Description("Channel as an @handle, UC… id or URL.")] string channel,
        [Description("Only report videos published in the last N days, 1-365. Default 30.")] int? publishedWithinDays = null,
        [Description("Optional: short or longform.")] string? format = null,
        [Description("Minimum score to report (video ÷ baseline). Default 1.5.")] double? minRatio = null,
        [Description("How many recent uploads to use as the baseline, 10-200. Default 50.")] int? maxVideos = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analysis.FindOutliersAsync(
            new OutlierQuery(channel, publishedWithinDays ?? 30, ChannelTools.ParseFormat(format), maxVideos ?? 50, minRatio ?? 1.5),
            cancellationToken));
}
