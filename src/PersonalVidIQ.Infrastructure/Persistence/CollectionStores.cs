using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class CollectionStateStore(IDbContextFactory<PersonalVidIQDbContext> contextFactory) : ICollectionStateStore
{
    public async Task<IReadOnlyList<TrackedChannelState>> GetTrackedChannelsAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Channels.AsNoTracking()
            .Where(c => c.IsTracked)
            .Select(c => new TrackedChannelState(
                c.YouTubeChannelId,
                c.Title,
                c.UploadsPlaylistId,
                c.Snapshots.Max(s => (DateTimeOffset?)s.CapturedAtUtc),
                c.LastUploadsCheckAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackedVideoState>> GetTrackedVideosAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Videos.AsNoTracking()
            .Where(v => v.IsTracked)
            .Select(v => new TrackedVideoState(
                v.YouTubeVideoId,
                v.Channel != null ? v.Channel.YouTubeChannelId : null,
                v.PublishedAtUtc,
                v.Snapshots.Max(s => (DateTimeOffset?)s.CapturedAtUtc)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetStoredVideoIdsAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken)
    {
        if (videoIds.Count == 0)
        {
            return new HashSet<string>();
        }

        var ids = videoIds.ToList();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await db.Videos.AsNoTracking().Where(v => ids.Contains(v.YouTubeVideoId)).Select(v => v.YouTubeVideoId).ToListAsync(cancellationToken);
        return stored.ToHashSet(StringComparer.Ordinal);
    }
}

public sealed class CollectionRunLog(IDbContextFactory<PersonalVidIQDbContext> contextFactory, TimeProvider timeProvider) : ICollectionRunLog
{
    public async Task<long> StartAsync(string trigger, bool forced, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = new CollectionRun { StartedAtUtc = timeProvider.GetUtcNow(), Trigger = trigger, Forced = forced, Status = "Running" };
        db.CollectionRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task CompleteAsync(long runId, CollectionRun result, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.CollectionRuns.SingleAsync(r => r.Id == runId, cancellationToken);
        run.CompletedAtUtc = timeProvider.GetUtcNow();
        run.Status = result.Status;
        run.ChannelsRefreshed = result.ChannelsRefreshed;
        run.VideosRefreshed = result.VideosRefreshed;
        run.NewVideosTracked = result.NewVideosTracked;
        run.SnapshotsSaved = result.SnapshotsSaved;
        run.UnitsUsed = result.UnitsUsed;
        run.Error = result.Error is { Length: > 2000 } e ? e[..2000] : result.Error;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    public async Task<CollectionRun?> GetLastAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CollectionRuns.AsNoTracking().OrderByDescending(r => r.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
    }
}
