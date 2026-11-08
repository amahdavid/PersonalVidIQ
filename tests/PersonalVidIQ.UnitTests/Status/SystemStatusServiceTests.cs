using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Collection;
using PersonalVidIQ.Application.DTOs;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Domain.Entities;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Infrastructure.Status;
using PersonalVidIQ.Infrastructure.YouTube;

namespace PersonalVidIQ.UnitTests.Status;

public sealed class SystemStatusServiceTests
{
    private sealed class FakeProbe(DatabaseStatus status) : IDatabaseProbe
    {
        public Task<DatabaseStatus> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(status);
    }

    private sealed class FakeStorage : IStorageProbe
    {
        public Task<StorageStatus> GetStorageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new StorageStatus([], 1, 2, 3, 4, 5, null));
    }

    private sealed class EmptyCollectionState : ICollectionStateStore
    {
        public Task<IReadOnlyList<TrackedChannelState>> GetTrackedChannelsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackedChannelState>>([]);
        public Task<IReadOnlyList<TrackedVideoState>> GetTrackedVideosAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackedVideoState>>([]);
        public Task<IReadOnlySet<string>> GetStoredVideoIdsAsync(IReadOnlyCollection<string> videoIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }

    private sealed class EmptyRunLog : ICollectionRunLog
    {
        public Task<long> StartAsync(string trigger, bool forced, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task CompleteAsync(long runId, CollectionRun result, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CollectionRun?> GetLastAsync(CancellationToken cancellationToken) => Task.FromResult<CollectionRun?>(null);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static SystemStatusService Create(string? apiKey, DatabaseStatus db)
    {
        var youTube = Options.Create(new YouTubeOptions { ApiKey = apiKey });
        var time = new FixedTime(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        return new(
            youTube,
            Options.Create(new DataCollectionOptions()),
            new FakeProbe(db),
            new FakeStorage(),
            new InMemoryQuotaLedger(youTube, time),
            new EmptyCollectionState(),
            new EmptyRunLog(),
            time);
    }

    [Fact]
    public async Task No_problems_when_key_set_and_database_reachable()
    {
        var status = await Create("key", new DatabaseStatus(true, "17.11", null)).GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(status.YouTubeApiKeyConfigured);
        Assert.Empty(status.Problems);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero), status.CheckedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_api_key_is_reported(string? apiKey)
    {
        var status = await Create(apiKey, new DatabaseStatus(true, "17.11", null)).GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.False(status.YouTubeApiKeyConfigured);
        Assert.Contains(status.Problems, p => p.Contains("YouTube API key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unreachable_database_is_reported_with_fix()
    {
        var status = await Create("key", new DatabaseStatus(false, null, "connection refused")).GetStatusAsync(TestContext.Current.CancellationToken);

        var problem = Assert.Single(status.Problems);
        Assert.Contains("connection refused", problem, StringComparison.Ordinal);
        Assert.Contains("brew services start postgresql@17", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settings_reflect_policy_defaults()
    {
        var status = await Create("key", new DatabaseStatus(true, "17.11", null)).GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(30, status.Settings.SnapshotRetentionDays);
        Assert.Equal(180, status.Settings.ShortsMaxDurationSeconds);
        Assert.Equal(50, status.Settings.DailySearchCallCap);
        Assert.Equal(24, status.Settings.SearchCacheHours);
    }
}
