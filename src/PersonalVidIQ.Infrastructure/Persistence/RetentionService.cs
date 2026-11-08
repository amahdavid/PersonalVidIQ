using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.Infrastructure.Persistence;

/// <summary>
/// Deletes stored YouTube data that is past its allowed age:
/// snapshots older than the retention window, untracked videos/channels not refreshed within
/// StaleMetadataDays, expired search cache rows, and old quota bookkeeping.
/// </summary>
public sealed class RetentionService(
    IDbContextFactory<PersonalVidIQDbContext> contextFactory,
    IOptions<DataCollectionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionService> logger) : IRetentionService
{
    public async Task<RetentionResult> PurgeAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var now = timeProvider.GetUtcNow();
        var snapshotCutoff = now.AddDays(-o.SnapshotRetentionDays);
        var staleCutoff = now.AddDays(-o.StaleMetadataDays);
        var quotaCutoff = now.AddDays(-o.QuotaHistoryDays);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var videoSnapshots = await db.VideoSnapshots.Where(s => s.CapturedAtUtc < snapshotCutoff).ExecuteDeleteAsync(cancellationToken);
        var channelSnapshots = await db.ChannelSnapshots.Where(s => s.CapturedAtUtc < snapshotCutoff).ExecuteDeleteAsync(cancellationToken);
        var videos = await db.Videos.Where(v => !v.IsTracked && v.LastRefreshedAtUtc < staleCutoff).ExecuteDeleteAsync(cancellationToken);
        var channels = await db.Channels
            .Where(c => !c.IsTracked && c.LastRefreshedAtUtc < staleCutoff && !c.Videos.Any())
            .ExecuteDeleteAsync(cancellationToken);
        var searchCache = await db.SearchCache.Where(s => s.ExpiresAtUtc <= now).ExecuteDeleteAsync(cancellationToken);
        var quota = await db.QuotaUsage.Where(q => q.QuotaDayStartUtc < quotaCutoff).ExecuteDeleteAsync(cancellationToken);
        await db.CollectionRuns.Where(r => r.StartedAtUtc < quotaCutoff).ExecuteDeleteAsync(cancellationToken);

        var result = new RetentionResult(videoSnapshots, channelSnapshots, videos, channels, searchCache, quota, now);
        if (result.Total > 0)
        {
            logger.LogInformation(
                "Retention purge: {VideoSnapshots} video snapshots, {ChannelSnapshots} channel snapshots, {Videos} stale videos, {Channels} stale channels, {Search} search cache rows, {Quota} quota rows",
                videoSnapshots, channelSnapshots, videos, channels, searchCache, quota);
        }

        return result;
    }
}
