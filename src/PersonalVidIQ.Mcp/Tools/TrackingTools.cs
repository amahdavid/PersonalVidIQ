using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class TrackingTools(ITrackingService tracking, ICollectionService collection)
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

    [McpServerTool(Name = "refresh_tracked", Title = "Refresh tracked items", Destructive = false, OpenWorld = true)]
    [Description("Refresh what's due: snapshot tracked channels (daily) and videos (every 3 h for the first 48 h, then daily, weekly after 30 days), " +
                 "pick up new uploads from tracked channels, and untrack videos that were deleted. This is how history for velocity and growth gets built; " +
                 "run it whenever you start a session. Stays within today's quota, keeping 1,000 units in reserve. Usually 1-10 quota units. " +
                 "Use dryRun to see what's due and the estimated cost without fetching anything.")]
    public Task<CollectionReport> RefreshTracked(
        [Description("Refresh everything tracked now, even if not due. Default false.")] bool? force = null,
        [Description("Only report what would be refreshed and the estimated quota cost. Default false.")] bool? dryRun = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => collection.CollectAsync(new CollectRequest("mcp", force ?? false, dryRun ?? false), cancellationToken));

    [McpServerTool(Name = "list_tracked", Title = "List tracked items", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List tracked channels (with how many of their videos are tracked) and individually tracked videos. No quota used.")]
    public Task<TrackedList> ListTracked(CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => tracking.ListTrackedAsync(cancellationToken));
}
