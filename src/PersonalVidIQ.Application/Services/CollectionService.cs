using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;
using PersonalVidIQ.Domain.Entities;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Application.Services;

/// <summary>
/// On-demand collector (DECISIONS.md #6). Snapshots tracked channels and videos that are due, picks up new
/// uploads from tracked channels, untracks videos that disappeared, and stays inside the day's quota budget.
/// Uses real elapsed time, so gaps from a sleeping laptop are fine.
/// </summary>
public sealed class CollectionService(
    IYouTubeDataClient youTube,
    ICollectionStateStore state,
    ICollectionRunLog runLog,
    IChannelStore channelStore,
    IVideoStore videoStore,
    ISnapshotStore snapshots,
    IRetentionService retention,
    IQuotaLedger quotaLedger,
    IOptions<DataCollectionOptions> options,
    TimeProvider timeProvider,
    ILogger<CollectionService> logger) : ICollectionService
{
    private const int MaxNewUploadsListed = 20;

    public async Task<CollectionReport> CollectAsync(CollectRequest request, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var policy = new DuePolicy(o);
        var purge = request.DryRun ? null : await retention.PurgeAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var channels = await state.GetTrackedChannelsAsync(cancellationToken);
        var videos = await state.GetTrackedVideosAsync(cancellationToken);
        var plan = CollectionPlanner.Plan(channels, videos, policy, now, request.Force);
        var usageBefore = await quotaLedger.GetUsageAsync(cancellationToken);
        var notes = new List<string>();

        // Budget: leave QuotaReserveUnits for searches and lookups. Channel work is cheap; trim videos (oldest tier last) if needed.
        var budget = Math.Max(0, usageBefore.UnitsRemaining - o.QuotaReserveUnits);
        var channelCost = Batches(plan.ChannelsToSnapshot.Count) + plan.ChannelsToCheckForUploads.Count + (plan.ChannelsToCheckForUploads.Count > 0 ? 1 : 0);
        var doChannels = channelCost <= budget;
        var videoBudget = budget - (doChannels ? channelCost : 0);
        var videosToRefresh = plan.VideosToSnapshot.Take(Math.Max(0, videoBudget) * 50).ToList();
        var skippedForQuota = plan.VideosToSnapshot.Count - videosToRefresh.Count;
        if (!doChannels || skippedForQuota > 0)
        {
            notes.Add($"Quota budget: {budget} units available after keeping {o.QuotaReserveUnits} in reserve; some work was skipped and stays due.");
        }

        if (request.DryRun)
        {
            notes.Insert(0, "Dry run: nothing was fetched or saved.");
            return Report("Planned", 0, 0, [], new Dictionary<string, int>(), [], 0, usageBefore.UnitsRemaining);
        }

        if (plan.ChannelsToSnapshot.Count + plan.ChannelsToCheckForUploads.Count + videosToRefresh.Count == 0)
        {
            notes.Add(channels.Count + videos.Count == 0
                ? "Nothing is tracked yet. Use track_channel or track_video."
                : "Nothing is due yet.");
        }

        var runId = await runLog.StartAsync(request.Trigger, request.Force, cancellationToken);
        var channelsSnapshotted = 0;
        var uploadChecks = 0;
        var saved = 0;
        var newUploads = new List<NewUpload>();
        var byTier = new Dictionary<string, int>();
        var gone = new List<string>();
        string status = "Succeeded";
        string? error = null;

        try
        {
            if (doChannels && plan.ChannelsToSnapshot.Count > 0)
            {
                var fresh = await youTube.GetChannelsAsync(plan.ChannelsToSnapshot.Select(c => c.ChannelId).ToList(), cancellationToken);
                foreach (var channel in fresh)
                {
                    await channelStore.UpsertAsync(channel, cancellationToken);
                }

                saved += await snapshots.AddChannelSnapshotsAsync(fresh, cancellationToken);
                channelsSnapshotted = fresh.Count;
                foreach (var missing in plan.ChannelsToSnapshot.Where(c => fresh.All(f => f.Id != c.ChannelId)))
                {
                    notes.Add($"Channel '{missing.Title}' wasn't returned by YouTube (deleted or hidden?). It's still tracked; untrack it if it's gone.");
                }
            }

            if (doChannels && plan.ChannelsToCheckForUploads.Count > 0)
            {
                var candidates = new List<string>();
                foreach (var channel in plan.ChannelsToCheckForUploads)
                {
                    try
                    {
                        var latest = await youTube.GetPlaylistVideosAsync(channel.UploadsPlaylistId!, o.UploadsCheckDepth, cancellationToken);
                        candidates.AddRange(latest.Select(v => v.VideoId));
                        await channelStore.MarkUploadsCheckedAsync(channel.ChannelId, cancellationToken);
                        uploadChecks++;
                    }
                    catch (YouTubeApiException ex) when (ex.Kind == YouTubeErrorKind.NotFound)
                    {
                        notes.Add($"Couldn't read uploads for '{channel.Title}': {ex.Message}");
                    }
                }

                var stored = await state.GetStoredVideoIdsAsync(candidates, cancellationToken);
                var newIds = candidates.Where(id => !stored.Contains(id)).Distinct().ToList();
                if (newIds.Count > 0)
                {
                    var batch = await youTube.GetVideosAsync(newIds, cancellationToken);
                    await videoStore.UpsertAsync(batch.Videos, TrackingSource.Channel, cancellationToken);
                    saved += await snapshots.AddVideoSnapshotsAsync(batch.Videos, cancellationToken);
                    newUploads.AddRange(batch.Videos.OrderByDescending(v => v.PublishedAtUtc)
                        .Select(v => new NewUpload(v.Id, v.Title, v.ChannelTitle, v.PublishedAtUtc)));
                }
            }

            if (videosToRefresh.Count > 0)
            {
                var batch = await youTube.GetVideosAsync(videosToRefresh.Select(v => v.Video.VideoId).ToList(), cancellationToken);
                await videoStore.UpsertAsync(batch.Videos, null, cancellationToken);
                saved += await snapshots.AddVideoSnapshotsAsync(batch.Videos, cancellationToken);
                var found = batch.Videos.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var (video, tier) in videosToRefresh.Where(v => found.Contains(v.Video.VideoId)))
                {
                    var key = tier.ToString().ToLowerInvariant();
                    byTier[key] = byTier.GetValueOrDefault(key) + 1;
                }

                foreach (var missing in batch.MissingIds)
                {
                    await videoStore.SetTrackedAsync(missing, false, cancellationToken);
                    gone.Add(missing);
                }

                if (gone.Count > 0)
                {
                    notes.Add($"{gone.Count} tracked videos are no longer public (deleted or made private) and were untracked.");
                }
            }
        }
        catch (YouTubeApiException ex) when (ex.Kind is YouTubeErrorKind.QuotaExceeded or YouTubeErrorKind.Transient)
        {
            status = "Partial";
            error = ex.Message;
            notes.Add($"Stopped early: {ex.Message} Everything collected before that was saved.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Collection run failed");
            await runLog.CompleteAsync(runId, new CollectionRun { Trigger = request.Trigger, Status = "Failed", Error = ex.Message }, CancellationToken.None);
            throw;
        }

        var usageAfter = await quotaLedger.GetUsageAsync(cancellationToken);
        var unitsUsed = Math.Max(0, usageAfter.UnitsUsed - usageBefore.UnitsUsed);
        await runLog.CompleteAsync(runId, new CollectionRun
        {
            Trigger = request.Trigger,
            Status = status,
            ChannelsRefreshed = channelsSnapshotted,
            VideosRefreshed = byTier.Values.Sum(),
            NewVideosTracked = newUploads.Count,
            SnapshotsSaved = saved,
            UnitsUsed = unitsUsed,
            Error = error,
        }, cancellationToken);

        notes.Add("Velocity needs at least two snapshots per video; run refresh_tracked again after the next due time to keep history building.");
        return Report(status, channelsSnapshotted, uploadChecks, newUploads, byTier, gone, saved, usageAfter.UnitsRemaining, unitsUsed);

        CollectionReport Report(
            string reportStatus, int snapshotted, int checks, List<NewUpload> uploads, Dictionary<string, int> tiers,
            List<string> unavailable, int snapshotsSaved, int remaining, int used = 0) => new(
            now,
            reportStatus,
            request.Force,
            request.DryRun,
            channels.Count,
            videos.Count,
            snapshotted,
            checks,
            uploads.Take(MaxNewUploadsListed).ToList(),
            tiers,
            plan.VideosNotDue,
            skippedForQuota,
            unavailable,
            snapshotsSaved,
            plan.EstimatedUnits,
            used,
            remaining,
            plan.NextDueUtc,
            purge,
            notes);
    }

    private static int Batches(int count) => (count + 49) / 50;
}
