using System.Reflection;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.DTOs;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.Persistence;

namespace PersonalVidIQ.Infrastructure.Status;

public sealed class SystemStatusService(
    IOptions<YouTubeOptions> youTube,
    IOptions<DataCollectionOptions> collection,
    IDatabaseProbe databaseProbe,
    IStorageProbe storageProbe,
    IQuotaLedger quotaLedger,
    ICollectionStateStore collectionState,
    ICollectionRunLog runLog,
    TimeProvider timeProvider) : ISystemStatusService
{
    public async Task<SystemStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var yt = youTube.Value;
        var dc = collection.Value;
        var database = await databaseProbe.ProbeAsync(cancellationToken);

        var problems = new List<string>();
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(yt.ApiKey);
        if (!apiKeyConfigured)
        {
            problems.Add("YouTube API key is not set. Run: dotnet user-secrets set \"YouTube:ApiKey\" \"<key>\" --project src/PersonalVidIQ.Host");
        }

        if (!database.Reachable)
        {
            problems.Add($"PostgreSQL is not reachable: {database.Error}. Check that it is running: brew services start postgresql@17");
        }

        StorageStatus? storage = null;
        CollectionStatus? collectionStatus = null;
        QuotaUsage quota;
        if (database.Reachable)
        {
            storage = await storageProbe.GetStorageAsync(cancellationToken);
            if (storage.PendingMigrations.Count > 0)
            {
                problems.Add($"Database schema is out of date ({storage.PendingMigrations.Count} pending migrations). Run: personalvidiq migrate");
                quota = EmptyQuota(yt);
            }
            else
            {
                quota = await quotaLedger.GetUsageAsync(cancellationToken);
                collectionStatus = await CollectionAsync(dc, cancellationToken);
            }
        }
        else
        {
            quota = EmptyQuota(yt);
        }

        var version = typeof(SystemStatusService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown";

        return new SystemStatus(
            version,
            timeProvider.GetUtcNow(),
            apiKeyConfigured,
            database,
            storage,
            collectionStatus,
            new SettingsSummary(
                dc.SnapshotRetentionDays,
                dc.ShortsMaxDurationSeconds,
                yt.DailySearchCallCap,
                yt.SearchCacheHours,
                yt.DailyQuotaUnits),
            quota,
            problems);
    }

    private async Task<CollectionStatus> CollectionAsync(DataCollectionOptions dc, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var plan = Application.Collection.CollectionPlanner.Plan(
            await collectionState.GetTrackedChannelsAsync(cancellationToken),
            await collectionState.GetTrackedVideosAsync(cancellationToken),
            new Application.Collection.DuePolicy(dc),
            now,
            force: false);
        var last = await runLog.GetLastAsync(cancellationToken);
        return new CollectionStatus(
            plan.ChannelsToSnapshot.Count,
            plan.ChannelsToCheckForUploads.Count,
            plan.VideosToSnapshot.Count,
            plan.NextDueUtc,
            last?.StartedAtUtc,
            last?.Status,
            last?.UnitsUsed,
            last?.Error);
    }

    private QuotaUsage EmptyQuota(YouTubeOptions yt)
    {
        var dayStart = YouTube.QuotaDay.StartUtc(timeProvider.GetUtcNow());
        return new QuotaUsage(dayStart, YouTube.QuotaDay.NextStartUtc(dayStart), 0, yt.DailyQuotaUnits, 0, yt.DailySearchCallCap, new Dictionary<string, int>());
    }
}
