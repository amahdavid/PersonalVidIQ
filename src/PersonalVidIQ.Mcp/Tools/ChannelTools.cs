using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class ChannelTools(IResearchService research, IChannelResearchService channels)
{
    private const string ChannelParam = "Channel as an @handle, a UC… channel id, or a channel URL.";

    [McpServerTool(Name = "get_channel", Title = "Get channel details", ReadOnly = true, OpenWorld = true)]
    [Description("Get a channel's public details and statistics (subscribers, total views, video count). 1 quota unit.")]
    public Task<ChannelReport> GetChannel(
        [Description(ChannelParam)] string channel,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => research.GetChannelAsync(channel, cancellationToken));

    [McpServerTool(Name = "get_channel_videos", Title = "List a channel's videos", ReadOnly = true, OpenWorld = true)]
    [Description("List a channel's recent uploads with public stats and lifetime views/day. 'popular' ranks the latest 200 uploads by views " +
                 "(YouTube can't sort a whole channel by popularity cheaply). About 3 quota units (up to 9 for popular or a format filter).")]
    public Task<ChannelVideosReport> GetChannelVideos(
        [Description(ChannelParam)] string channel,
        [Description("How many videos to return, 1-50. Default 20.")] int? maxResults = null,
        [Description("recent (default) or popular.")] string? order = null,
        [Description("Optional: short (180 s or less) or longform.")] string? format = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => research.GetChannelVideosAsync(
            channel,
            maxResults ?? 20,
            ToolGuard.ParseEnum(order, ChannelVideoOrder.Recent, "order"),
            ParseFormat(format),
            cancellationToken));

    [McpServerTool(Name = "compare_channels", Title = "Compare channels", ReadOnly = true, OpenWorld = true)]
    [Description("Compare up to 5 channels: public stats, upload rate, Shorts vs long-form mix, typical views/day per format, and growth " +
                 "from stored snapshots over the period. About 3 quota units per channel.")]
    public Task<ChannelComparison> CompareChannels(
        [Description("1-5 channels (@handles, UC… ids or URLs).")] string[] channelList,
        [Description("Period in days, 1-30. Default 7.")] int? periodDays = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => channels.CompareChannelsAsync(channelList, periodDays ?? 7, cancellationToken));

    internal static VideoFormat? ParseFormat(string? format) =>
        string.IsNullOrWhiteSpace(format) ? null : ToolGuard.ParseEnum(format, VideoFormat.Unknown, "format") switch
        {
            VideoFormat.Unknown => throw new ModelContextProtocol.McpException("Invalid format. Use short or longform."),
            var f => f,
        };
}
