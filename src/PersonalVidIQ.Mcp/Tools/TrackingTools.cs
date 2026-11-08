using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class TrackingTools(ITrackingService tracking)
{
    [McpServerTool(Name = "track_channel", Title = "Track a channel", Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Start tracking a channel: saves its stats now, tracks its latest 50 uploads, and picks up new uploads on each refresh. " +
                 "Tracking builds the snapshot history used for velocity and growth (kept 30 days). About 3 quota units.")]
    public Task<TrackChannelResult> TrackChannel(
        [Description("Channel as an @handle, UC… id or URL.")] string channel,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => tracking.TrackChannelAsync(channel, cancellationToken));

    [McpServerTool(Name = "untrack_channel", Title = "Stop tracking a channel", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Stop tracking a channel and the videos it brought in. Videos tracked individually stay tracked. Stored data ages out within 30 days.")]
    public Task<UntrackResult> UntrackChannel(
        [Description("Channel as an @handle, UC… id or URL.")] string channel,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => tracking.UntrackChannelAsync(channel, cancellationToken));

    [McpServerTool(Name = "track_video", Title = "Track a video", Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Start tracking one video and save its stats now, so later refreshes can measure its views/day. 1-2 quota units.")]
    public Task<TrackVideoResult> TrackVideo(
        [Description("Video id or URL.")] string video,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => tracking.TrackVideoAsync(video, cancellationToken));

    [McpServerTool(Name = "untrack_video", Title = "Stop tracking a video", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Stop tracking one video. Stored data ages out within 30 days.")]
    public Task<UntrackResult> UntrackVideo(
        [Description("Video id or URL.")] string video,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => tracking.UntrackVideoAsync(video, cancellationToken));

    [McpServerTool(Name = "list_tracked", Title = "List tracked items", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List tracked channels (with how many of their videos are tracked) and individually tracked videos. No quota used.")]
    public Task<TrackedList> ListTracked(CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => tracking.ListTrackedAsync(cancellationToken));
}
