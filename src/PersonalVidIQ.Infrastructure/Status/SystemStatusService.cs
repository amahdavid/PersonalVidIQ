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

        var version = typeof(SystemStatusService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown";

        return new SystemStatus(
            version,
            timeProvider.GetUtcNow(),
            apiKeyConfigured,
            database,
            new SettingsSummary(
                dc.SnapshotRetentionDays,
                dc.ShortsMaxDurationSeconds,
                yt.DailySearchCallCap,
                yt.SearchCacheHours,
                yt.DailyQuotaUnits),
            problems);
    }
}
