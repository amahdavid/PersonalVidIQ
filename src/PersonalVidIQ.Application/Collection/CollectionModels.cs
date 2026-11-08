namespace PersonalVidIQ.Application.Collection;

/// <summary>A tracked video as the collector sees it.</summary>
public sealed record TrackedVideoState(string VideoId, string? ChannelId, DateTimeOffset? PublishedAtUtc, DateTimeOffset? LastSnapshotUtc);

/// <summary>A tracked channel as the collector sees it.</summary>
public sealed record TrackedChannelState(
    string ChannelId,
    string Title,
    string? UploadsPlaylistId,
    DateTimeOffset? LastSnapshotUtc,
    DateTimeOffset? LastUploadsCheckUtc);

public sealed record CollectionPlan(
    DateTimeOffset AsOfUtc,
    IReadOnlyList<TrackedChannelState> ChannelsToSnapshot,
    IReadOnlyList<TrackedChannelState> ChannelsToCheckForUploads,
    IReadOnlyList<(TrackedVideoState Video, VideoTier Tier)> VideosToSnapshot,
    int VideosNotDue,
    DateTimeOffset? NextDueUtc)
{
    /// <summary>
    /// Units the plan needs: channels.list and videos.list take 50 ids per unit, and each uploads check is
    /// 1 unit plus its share of a videos.list call for new uploads. Upper bound; new uploads may add a little.
    /// </summary>
    public int EstimatedUnits =>
        Batches(ChannelsToSnapshot.Count) + ChannelsToCheckForUploads.Count + Batches(VideosToSnapshot.Count) + (ChannelsToCheckForUploads.Count > 0 ? 1 : 0);

    private static int Batches(int count) => (count + 49) / 50;
}

public static class CollectionPlanner
{
    /// <param name="force">Collect every tracked item now, ignoring due times (the 15-minute snapshot spacing still applies).</param>
    public static CollectionPlan Plan(
        IReadOnlyList<TrackedChannelState> channels,
        IReadOnlyList<TrackedVideoState> videos,
        DuePolicy policy,
        DateTimeOffset nowUtc,
        bool force)
    {
        var dueTimes = new List<DateTimeOffset?>();

        var channelSnapshots = channels.Where(c => force || policy.IsDue(c.LastSnapshotUtc, policy.ChannelInterval, nowUtc)).ToList();
        var uploadChecks = channels
            .Where(c => !string.IsNullOrEmpty(c.UploadsPlaylistId))
            .Where(c => force || policy.IsDue(c.LastUploadsCheckUtc, policy.UploadsCheckInterval, nowUtc))
            .ToList();
        dueTimes.AddRange(channels.Select(c => policy.NextDueUtc(c.LastSnapshotUtc, policy.ChannelInterval, nowUtc)));
        dueTimes.AddRange(channels.Select(c => policy.NextDueUtc(c.LastUploadsCheckUtc, policy.UploadsCheckInterval, nowUtc)));

        var due = new List<(TrackedVideoState, VideoTier)>();
        var notDue = 0;
        foreach (var video in videos)
        {
            var tier = policy.TierFor(video.PublishedAtUtc, nowUtc);
            var interval = policy.IntervalFor(tier);
            if (force || policy.IsDue(video.LastSnapshotUtc, interval, nowUtc))
            {
                due.Add((video, tier));
            }
            else
            {
                notDue++;
                dueTimes.Add(policy.NextDueUtc(video.LastSnapshotUtc, interval, nowUtc));
            }
        }

        // Newest videos first: they change fastest and matter most if quota runs short.
        due = due.OrderBy(d => d.Item2).ThenByDescending(d => d.Item1.PublishedAtUtc).ToList();
        var next = dueTimes.Where(d => d is not null).Select(d => d!.Value).DefaultIfEmpty().Min();

        return new CollectionPlan(nowUtc, channelSnapshots, uploadChecks, due, notDue, next == default ? null : next);
    }
}
